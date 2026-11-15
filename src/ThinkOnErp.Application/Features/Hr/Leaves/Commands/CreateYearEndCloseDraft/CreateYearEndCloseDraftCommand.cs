using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateYearEndCloseDraft;

public sealed record CreateYearEndCloseDraftCommand(YearEndRolloverDto Dto, string User) : IRequest<ApiResponse<LeaveYearCloseDto>>;

public sealed class CreateYearEndCloseDraftCommandValidator : AbstractValidator<CreateYearEndCloseDraftCommand>
{
    public CreateYearEndCloseDraftCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.FromYear).InclusiveBetween(1, 9998);
        RuleFor(x => x.Dto.ToYear).Equal(x => x.Dto.FromYear + 1);
    }
}

public sealed class CreateYearEndCloseDraftCommandHandler : IRequestHandler<CreateYearEndCloseDraftCommand, ApiResponse<LeaveYearCloseDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public CreateYearEndCloseDraftCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveYearCloseDto>> Handle(CreateYearEndCloseDraftCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.FromYear < 1 || dto.FromYear > 9998 || dto.ToYear != dto.FromYear + 1)
            throw new ArgumentException("Leave-year close must move to the immediately following cycle year.");

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "*" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var closeKey = $"{dto.FromYear}:{dto.ToYear}:{leaveTypeCode}";
        var existing = await _leaveRepo.GetLeaveYearCloseByKeyAsync(closeKey, cancellationToken);
        if (existing?.Status == "POSTED")
            return ApiResponse<LeaveYearCloseDto>.CreateSuccess(LeaveCommon.MapLeaveYearClose(existing), "Leave-year close draft prepared.");

        var balances = await _leaveRepo.GetLeaveBalancesByYearAsync(dto.FromYear, leaveTypeCode == "*" ? null : leaveTypeCode, cancellationToken);
        var close = new LeaveYearClose
        {
            Id = existing?.Id ?? 0,
            CloseKey = closeKey,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            LeaveTypeCode = leaveTypeCode,
            Status = "DRAFT",
            CreationUser = existing?.CreationUser ?? request.User,
            CreationDate = existing?.CreationDate ?? DateTime.UtcNow
        };

        foreach (var balance in balances.OrderBy(b => b.EmployeeCode, StringComparer.Ordinal).ThenBy(b => b.LeaveTypeCode, StringComparer.Ordinal).ThenBy(b => b.Id))
        {
            var leaveType = balance.LeaveType ?? await _leaveRepo.GetLeaveTypeByCodeAsync(balance.LeaveTypeCode, cancellationToken);
            if (leaveType == null || !leaveType.TracksBalance) continue;
            var unused = balance.AccruedDays + balance.CarriedForwardDays - balance.UsedDays;
            if (unused < 0m)
                throw new InvalidOperationException($"Negative leave balance for {balance.EmployeeCode}/{balance.LeaveTypeCode}; correct it before drafting the close.");

            var carried = leaveType.CarryForwardAllowed
                ? Math.Min(unused, leaveType.CarryForwardCapDays > 0m ? leaveType.CarryForwardCapDays : unused)
                : 0m;
            var excess = unused - carried;
            var action = LeaveCommon.NormalizeYearEndExcessAction(leaveType.YearEndExcessAction);
            if (action == "REQUIRE_USE" && excess > 0m)
                throw new InvalidOperationException($"{balance.EmployeeCode}/{balance.LeaveTypeCode} has {excess:F4} days that must be used before this leave-year can close.");
            var cashout = action == "CASHOUT" ? excess : 0m;
            var expired = action == "EXPIRE" ? excess : 0m;
            var sourceStart = balance.CycleStartDate ?? new DateTime(balance.YearNo, 1, 1);
            var sourceEnd = balance.CycleEndDate ?? new DateTime(balance.YearNo, 12, 31);
            if (await _leaveRepo.HasPendingLeaveRequestsAsync(balance.EmployeeCode, balance.LeaveTypeCode, sourceStart, sourceEnd, cancellationToken))
                throw new InvalidOperationException($"Resolve pending leave requests for {balance.EmployeeCode}/{balance.LeaveTypeCode} before drafting the close.");
            var targetStart = sourceEnd.AddDays(1);
            var targetEnd = targetStart.AddYears(1).AddDays(-1);
            var target = await _leaveRepo.GetLeaveBalanceByCycleAsync(balance.EmployeeCode, balance.LeaveTypeCode, targetStart, cancellationToken);

            close.Lines.Add(new LeaveYearCloseLine
            {
                EmployeeCode = balance.EmployeeCode,
                LeaveTypeCode = balance.LeaveTypeCode,
                SourceYear = balance.YearNo,
                TargetYear = targetStart.Year,
                SourceBalanceId = balance.Id,
                SourceCycleStartDate = sourceStart,
                SourceCycleEndDate = sourceEnd,
                TargetCycleStartDate = targetStart,
                TargetCycleEndDate = targetEnd,
                SourceAccruedBefore = balance.AccruedDays,
                SourceUsedDays = balance.UsedDays,
                SourceCarriedBefore = balance.CarriedForwardDays,
                TargetAccruedBefore = target?.AccruedDays ?? 0m,
                TargetCarriedBefore = target?.CarriedForwardDays ?? 0m,
                TargetUsedBefore = target?.UsedDays ?? 0m,
                UnusedDays = unused,
                CarriedDays = carried,
                ExpiredDays = expired,
                CashoutDays = cashout,
                TargetBalanceId = target?.Id,
                TracksBalanceSnapshot = leaveType.TracksBalance,
                CarryForwardAllowedSnapshot = leaveType.CarryForwardAllowed,
                CarryForwardCapDaysSnapshot = leaveType.CarryForwardCapDays,
                YearEndExcessActionSnapshot = action
            });
        }

        var savedClose = await _leaveRepo.SaveLeaveYearCloseDraftAsync(close, cancellationToken);
        var resultDto = LeaveCommon.MapLeaveYearClose(savedClose);
        return ApiResponse<LeaveYearCloseDto>.CreateSuccess(resultDto, "Leave-year close draft prepared.");
    }
}
