using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateBalanceMigrationDraft;

public sealed record CreateBalanceMigrationDraftCommand(CreateLeaveBalanceMigrationDto Dto, string User) : IRequest<ApiResponse<LeaveBalanceMigrationDto>>;

public sealed class CreateBalanceMigrationDraftCommandValidator : AbstractValidator<CreateBalanceMigrationDraftCommand>
{
    public CreateBalanceMigrationDraftCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.MigrationKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.TargetYearStartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.Dto.TargetYearStartDay).InclusiveBetween(1, 31);
        RuleFor(x => x.Dto.Allocations).NotEmpty();
    }
}

public sealed class CreateBalanceMigrationDraftCommandHandler : IRequestHandler<CreateBalanceMigrationDraftCommand, ApiResponse<LeaveBalanceMigrationDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public CreateBalanceMigrationDraftCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveBalanceMigrationDto>> Handle(CreateBalanceMigrationDraftCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var migrationKey = dto.MigrationKey?.Trim();
        if (string.IsNullOrWhiteSpace(migrationKey) || migrationKey.Length > 200)
            throw new ArgumentException("MigrationKey (1..200 characters) is required.", nameof(dto.MigrationKey));
        if (dto.EffectiveDate == default)
            throw new ArgumentException("An explicit effective date is required.", nameof(dto.EffectiveDate));
        if (dto.TargetYearStartMonth is < 1 or > 12 || dto.TargetYearStartDay < 1 ||
            dto.TargetYearStartDay > DateTime.DaysInMonth(2000, dto.TargetYearStartMonth))
            throw new ArgumentException("Target leave-year start month/day is invalid.");
        if (dto.Allocations == null || dto.Allocations.Count == 0 || dto.Allocations.Count > 10000)
            throw new ArgumentException("Between 1 and 10,000 explicit balance allocations are required.", nameof(dto.Allocations));
        if (dto.Allocations.Any(a => a is null))
            throw new ArgumentException("Allocation entries cannot be null.", nameof(dto.Allocations));

        var allocations = dto.Allocations.OrderBy(a => a.SourceBalanceId).ThenBy(a => a.TargetCycleStartDate).ToList();
        if (allocations.Any(a => a.SourceBalanceId <= 0 || a.AccruedDays != decimal.Round(a.AccruedDays, 4) ||
            a.UsedDays != decimal.Round(a.UsedDays, 4) || a.CarriedDays != decimal.Round(a.CarriedDays, 4)))
            throw new ArgumentException("Allocation source IDs must be positive and days may have at most four decimal places.", nameof(dto.Allocations));

        var payload = JsonSerializer.Serialize(new
        {
            MigrationKey = migrationKey,
            EffectiveDate = dto.EffectiveDate.Date,
            dto.TargetYearStartMonth,
            dto.TargetYearStartDay,
            Allocations = allocations.Select(a => new { a.SourceBalanceId, Start = a.TargetCycleStartDate.Date, End = a.TargetCycleEndDate.Date,
                Accrued = a.AccruedDays.ToString("G29", CultureInfo.InvariantCulture),
                Used = a.UsedDays.ToString("G29", CultureInfo.InvariantCulture),
                Carried = a.CarriedDays.ToString("G29", CultureInfo.InvariantCulture) })
        });
        var payloadHash = LeaveMovementKey.Hash(payload);
        var existingMigration = await _leaveRepo.GetLeaveBalanceMigrationByKeyAsync(migrationKey, cancellationToken);
        if (existingMigration != null)
        {
            if (existingMigration.PayloadHash != payloadHash)
                throw new InvalidOperationException("MigrationKey was already used for a different reviewed allocation plan.");
            return ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(LeaveCommon.MapLeaveBalanceMigration(existingMigration), "Leave-balance migration draft prepared for review.");
        }

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (settings.LeaveYearStartMonth == dto.TargetYearStartMonth && settings.LeaveYearStartDay == dto.TargetYearStartDay)
            throw new InvalidOperationException("The target leave-year start matches the current setting; no cycle migration is needed.");

        var balances = await _leaveRepo.GetAllLeaveBalancesForMigrationAsync(cancellationToken);
        if (balances.Count == 0)
            throw new InvalidOperationException("There are no existing leave balances to migrate.");
        var byId = balances.ToDictionary(b => b.Id);
        var groupedAllocations = allocations.GroupBy(a => a.SourceBalanceId).ToDictionary(g => g.Key, g => g.ToList());
        if (groupedAllocations.Keys.Any(id => !byId.ContainsKey(id)))
            throw new ArgumentException("An allocation refers to a balance outside the current tenant.", nameof(dto.Allocations));
        var missingSourceIds = byId.Keys.Where(id => !groupedAllocations.ContainsKey(id)).ToArray();
        if (missingSourceIds.Length > 0)
            throw new InvalidOperationException($"The reviewed plan must allocate every existing balance; {missingSourceIds.Length} balance row(s) are missing.");

        var targetKey = new Func<string, string, DateTime, string>((employee, leaveType, start) =>
            $"{employee}\u001f{leaveType}\u001f{start:yyyyMMdd}");
        var targets = new Dictionary<string, (LeaveBalance? Balance, DateTime Start, DateTime End)>();
        var migration = new LeaveBalanceMigration
        {
            MigrationKey = migrationKey,
            EffectiveDate = dto.EffectiveDate.Date,
            SourceYearStartMonth = settings.LeaveYearStartMonth,
            SourceYearStartDay = settings.LeaveYearStartDay,
            TargetYearStartMonth = dto.TargetYearStartMonth,
            TargetYearStartDay = dto.TargetYearStartDay,
            PayloadHash = payloadHash,
            Status = "DRAFT",
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        foreach (var (sourceId, sourceAllocations) in groupedAllocations.OrderBy(x => x.Key))
        {
            var source = byId[sourceId];
            var sourceStart = source.CycleStartDate?.Date ?? new DateTime(source.YearNo, 1, 1);
            var sourceEnd = source.CycleEndDate?.Date ?? new DateTime(source.YearNo, 12, 31);
            if (sourceEnd < sourceStart) throw new InvalidOperationException($"Source balance {source.Id} has an invalid cycle.");
            if (sourceAllocations.GroupBy(a => a.TargetCycleStartDate.Date).Any(g => g.Count() > 1))
                throw new ArgumentException($"Balance {source.Id} has duplicate allocations for one target cycle.", nameof(dto.Allocations));
            if (sourceAllocations.Sum(a => a.AccruedDays) != source.AccruedDays ||
                sourceAllocations.Sum(a => a.UsedDays) != source.UsedDays ||
                sourceAllocations.Sum(a => a.CarriedDays) != source.CarriedForwardDays)
                throw new InvalidOperationException($"Allocations for balance {source.Id} must preserve accrued, used and carried days exactly.");

            foreach (var allocation in sourceAllocations)
            {
                var targetStart = allocation.TargetCycleStartDate.Date;
                var targetEnd = allocation.TargetCycleEndDate.Date;
                if (targetStart.Year is < 1 or > 9998 || targetEnd < targetStart ||
                    targetStart.Month != dto.TargetYearStartMonth ||
                    targetStart.Day != Math.Min(dto.TargetYearStartDay, DateTime.DaysInMonth(targetStart.Year, dto.TargetYearStartMonth)))
                    throw new ArgumentException($"Target cycle for balance {source.Id} does not begin on the configured target leave-year boundary.", nameof(dto.Allocations));
                var nextStartYear = targetStart.Year + 1;
                var nextStart = new DateTime(nextStartYear, dto.TargetYearStartMonth,
                    Math.Min(dto.TargetYearStartDay, DateTime.DaysInMonth(nextStartYear, dto.TargetYearStartMonth)));
                if (targetEnd != nextStart.AddDays(-1))
                    throw new ArgumentException($"Target cycle for balance {source.Id} must end the day before its next configured annual boundary.", nameof(dto.Allocations));

                var key = targetKey(source.EmployeeCode, source.LeaveTypeCode, targetStart);
                if (!targets.ContainsKey(key))
                {
                    var target = await _leaveRepo.GetLeaveBalanceByCycleAsync(source.EmployeeCode, source.LeaveTypeCode, targetStart, cancellationToken);
                    if (target != null && (target.CycleEndDate?.Date ?? targetEnd) != targetEnd)
                        throw new InvalidOperationException($"Existing target balance {target.Id} has a different cycle end.");
                    targets.Add(key, (target, targetStart, targetEnd));
                }
                migration.Lines.Add(new LeaveBalanceMigrationLine
                {
                    SourceBalanceId = source.Id,
                    EmployeeCode = source.EmployeeCode,
                    LeaveTypeCode = source.LeaveTypeCode,
                    SourceCycleStartDate = sourceStart,
                    SourceCycleEndDate = sourceEnd,
                    SourceYearNo = source.YearNo,
                    SourceCycleStartWasNull = !source.CycleStartDate.HasValue,
                    SourceCycleEndWasNull = !source.CycleEndDate.HasValue,
                    SourceAccruedDays = source.AccruedDays,
                    SourceUsedDays = source.UsedDays,
                    SourceCarriedDays = source.CarriedForwardDays,
                    TargetCycleStartDate = targetStart,
                    TargetCycleEndDate = targetEnd,
                    AllocatedAccruedDays = allocation.AccruedDays,
                    AllocatedUsedDays = allocation.UsedDays,
                    AllocatedCarriedDays = allocation.CarriedDays,
                    TargetBalanceId = targets[key].Balance?.Id,
                    TargetAccruedBefore = targets[key].Balance?.AccruedDays ?? 0m,
                    TargetUsedBefore = targets[key].Balance?.UsedDays ?? 0m,
                    TargetCarriedBefore = targets[key].Balance?.CarriedForwardDays ?? 0m
                });
            }
        }

        var saved = await _leaveRepo.SaveLeaveBalanceMigrationDraftAsync(migration, cancellationToken);
        var resultDto = LeaveCommon.MapLeaveBalanceMigration(saved);
        return ApiResponse<LeaveBalanceMigrationDto>.CreateSuccess(resultDto, "Leave-balance migration draft prepared for review.");
    }
}
