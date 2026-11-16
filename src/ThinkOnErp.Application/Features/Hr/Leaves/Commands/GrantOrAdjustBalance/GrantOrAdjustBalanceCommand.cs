using System;
using System.Globalization;
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

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.GrantOrAdjustBalance;

public sealed record GrantOrAdjustBalanceCommand(GrantLeaveBalanceDto Dto, string User) : IRequest<ApiResponse<LeaveBalanceDto>>;

public sealed class GrantOrAdjustBalanceCommandValidator : AbstractValidator<GrantOrAdjustBalanceCommand>
{
    public GrantOrAdjustBalanceCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.LeaveTypeCode).NotEmpty();
        RuleFor(x => x.Dto.Days).NotEqual(0m);
        RuleFor(x => x.Dto.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class GrantOrAdjustBalanceCommandHandler : IRequestHandler<GrantOrAdjustBalanceCommand, ApiResponse<LeaveBalanceDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public GrantOrAdjustBalanceCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<LeaveBalanceDto>> Handle(GrantOrAdjustBalanceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var normalizedEmployeeCode = dto.EmployeeCode?.Trim() ?? string.Empty;
        if (normalizedEmployeeCode.Length == 0) throw new ArgumentException("EmployeeCode is required.", nameof(dto));

        var result = await _leaveRepo.ExecuteForLeaveEmployeeAsync(normalizedEmployeeCode,
            () => GrantOrAdjustCoreAsync(dto, request.User, cancellationToken), cancellationToken);

        return ApiResponse<LeaveBalanceDto>.CreateSuccess(result, "Leave balance granted/adjusted successfully.");
    }

    private async Task<LeaveBalanceDto> GrantOrAdjustCoreAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' not found.");

        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var hasPolicyHistory = await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, dto.LeaveTypeCode, cancellationToken);
        var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleForLabelAsync(_leaveRepo, emp, dto.LeaveTypeCode, dto.YearNo, leaveSettings, cancellationToken);
        if (policy == null && hasPolicyHistory)
            throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 200)
            throw new ArgumentException("IdempotencyKey (1..200 characters) is required for manual balance changes.");
        if (decimal.Round(dto.Days, 4) != dto.Days)
            throw new ArgumentException("Manual balance days must have at most four decimal places.");
        if (dto.Days > 99_999_999.9999m || dto.Days < -99_999_999.9999m)
            throw new ArgumentOutOfRangeException(nameof(dto.Days), "Manual balance days exceed the NUMBER(12,4) storage limit.");
        if (dto.Days == 0m) throw new ArgumentException("Manual balance change must be non-zero.");
        var adjustmentType = dto.AdjustmentType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (adjustmentType is not ("OPENING_BALANCE" or "MANUAL_GRANT" or "COMPENSATORY" or "CORRECTION"))
            throw new ArgumentException("AdjustmentType must be OPENING_BALANCE, MANUAL_GRANT, COMPENSATORY, or CORRECTION.");
        if (adjustmentType != "CORRECTION" && dto.Days < 0m)
            throw new ArgumentException("Only a CORRECTION may reduce the leave balance.");
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000)
            throw new ArgumentException("A reason from 1 to 1000 characters is required for manual balance changes.");
        var operationKey = LeaveMovementKey.Hash("MANUAL:" + dto.IdempotencyKey.Trim());
        var payload = LeaveMovementKey.Hash(JsonSerializer.Serialize(new { dto.EmployeeCode, dto.LeaveTypeCode, dto.YearNo,
            Days = dto.Days.ToString("G29", CultureInfo.InvariantCulture), Type = adjustmentType, Reason = reason }));
        var replay = await _leaveRepo.GetMovementByKeyAsync(operationKey, cancellationToken);
        if (replay != null)
        {
            if (replay.PayloadHash != payload) throw new InvalidOperationException("IdempotencyKey was already used for a different balance change.");
            var replayPending = await _leaveRepo.GetPendingLeaveDaysAsync(replay.EmployeeCode, replay.LeaveTypeCode,
                cycle.Start, cycle.End, cancellationToken: cancellationToken);
            var replayRemaining = replay.AccruedAfter + replay.CarriedAfter - replay.UsedAfter;
            return LeaveCommon.FormatBalance(new LeaveBalanceDto(replay.EmployeeCode, replay.LeaveTypeCode, leaveType.NameLocal, replay.CycleStartDate.Year,
                replay.AccruedAfter, replay.UsedAfter, replay.CarriedAfter, replayRemaining)
            { PendingDays = replayPending, AvailableDays = replayRemaining - replayPending }, leaveSettings);
        }
        await _leaveRepo.EnsureLeaveCycleOpenAsync(dto.EmployeeCode, dto.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken);

        var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(dto.EmployeeCode, dto.LeaveTypeCode, cycle.Start, cancellationToken);
        var before = LeaveBalanceState.From(balance);
        var pendingDays = await _leaveRepo.GetPendingLeaveDaysAsync(dto.EmployeeCode, dto.LeaveTypeCode,
            cycle.Start, cycle.End, cancellationToken: cancellationToken);
        if (balance == null)
        {
            if (dto.Days < 0m)
                throw new InvalidOperationException("A negative manual correction cannot create a balance.");
            if (dto.Days < pendingDays)
                throw new InvalidOperationException("Manual balance change would leave pending requests without reserved days.");
            balance = new LeaveBalance
            {
                EmployeeCode = dto.EmployeeCode,
                LeaveTypeCode = dto.LeaveTypeCode,
                YearNo = dto.YearNo,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = dto.Days,
                UsedDays = 0,
                CarriedForwardDays = 0,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };
            await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
        }
        else
        {
            balance.CycleStartDate ??= cycle.Start;
            balance.CycleEndDate ??= cycle.End;
            var resultingAccrued = adjustmentType == "OPENING_BALANCE" ? dto.Days : balance.AccruedDays + dto.Days;
            if (resultingAccrued < 0m || resultingAccrued + balance.CarriedForwardDays - balance.UsedDays < 0m)
                throw new InvalidOperationException("Manual balance change exceeds the available balance; review usage and dependent leave requests.");
            if (resultingAccrued + balance.CarriedForwardDays - balance.UsedDays < pendingDays)
                throw new InvalidOperationException("Manual balance change would leave pending requests without reserved days.");
            balance.AccruedDays = resultingAccrued;
            balance.UpdateUser = user;
            balance.UpdateDate = DateTime.UtcNow;
            await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
        }

        var movementKind = adjustmentType switch
        {
            "OPENING_BALANCE" => "OPENING_BALANCE",
            "CORRECTION" => "CORRECTION",
            "COMPENSATORY" => "COMPENSATORY",
            _ => "MANUAL_GRANT"
        };
        await _leaveRepo.RecordMovementAsync(balance, before, movementKind, operationKey, "MANUAL:" + dto.IdempotencyKey.Trim(),
            reason, user, payloadHash: payload, cancellationToken: cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        decimal remaining = (balance.AccruedDays + balance.CarriedForwardDays) - balance.UsedDays;
        var balanceDto = new LeaveBalanceDto(
            balance.EmployeeCode,
            balance.LeaveTypeCode,
            leaveType.NameLocal,
            balance.YearNo,
            balance.AccruedDays,
            balance.UsedDays,
            balance.CarriedForwardDays,
            remaining
        ) { PendingDays = pendingDays, AvailableDays = remaining - pendingDays };
        var displaySettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        return LeaveCommon.FormatBalance(balanceDto, displaySettings);
    }
}
