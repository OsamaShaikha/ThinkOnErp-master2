using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetEmployeeBalances;

public sealed record GetEmployeeBalancesQuery(string EmployeeCode, int? Year = null) : IRequest<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>;

public sealed class GetEmployeeBalancesQueryValidator : AbstractValidator<GetEmployeeBalancesQuery>
{
    public GetEmployeeBalancesQueryValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
    }
}

public sealed class GetEmployeeBalancesQueryHandler : IRequestHandler<GetEmployeeBalancesQuery, ApiResponse<IReadOnlyList<LeaveBalanceDto>>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public GetEmployeeBalancesQueryHandler(ILeaveRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<LeaveBalanceDto>>> Handle(GetEmployeeBalancesQuery request, CancellationToken cancellationToken)
    {
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var companyToday = LeaveCommon.GetLeaveCompanyToday(settings);
        var allTypes = await _leaveRepo.GetLeaveTypesAsync(true, cancellationToken);
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee '{request.EmployeeCode}' not found.");
        var result = new List<LeaveBalanceDto>();

        foreach (var type in allTypes.Where(t => t.TracksBalance))
        {
            var (policy, cycle) = request.Year.HasValue
                ? await LeaveCommon.ResolveLeavePolicyCycleForLabelAsync(_leaveRepo, employee, type.LeaveTypeCode, request.Year.Value, settings, cancellationToken)
                : await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, employee, type.LeaveTypeCode, companyToday, settings, cancellationToken);
            var noEffectivePolicy = policy == null && await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, type.LeaveTypeCode, cancellationToken);
            var targetYear = cycle.Start.Year;
            var asOfDate = request.Year.HasValue && companyToday > cycle.End ? cycle.End : companyToday;
            var bal = await _leaveRepo.GetLeaveBalanceByCycleAsync(request.EmployeeCode, type.LeaveTypeCode, cycle.Start, cancellationToken);

            var cycleClosed = bal == null && await _leaveRepo.IsLeaveCycleClosedAsync(request.EmployeeCode, type.LeaveTypeCode,
                cycle.Start, cycle.End, cancellationToken);
            decimal accrued = bal?.AccruedDays ?? (companyToday < cycle.Start || cycleClosed || noEffectivePolicy ? 0m : LeaveCommon.CalculateAccruedToDate(employee, type, policy, asOfDate, settings));
            decimal carried = bal?.CarriedForwardDays ?? 0m;
            decimal used = bal?.UsedDays ?? 0m;
            decimal remaining = accrued + carried - used;
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(request.EmployeeCode, type.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken: cancellationToken);

            var balanceDto = new LeaveBalanceDto(
                request.EmployeeCode,
                type.LeaveTypeCode,
                type.NameLocal,
                targetYear,
                accrued,
                used,
                carried,
                remaining
            )
            {
                CycleStartDate = bal?.CycleStartDate ?? cycle.Start,
                CycleEndDate = bal?.CycleEndDate ?? cycle.End,
                PendingDays = pending,
                AvailableDays = remaining - pending
            };
            result.Add(LeaveCommon.FormatBalance(balanceDto, settings));
        }

        return ApiResponse<IReadOnlyList<LeaveBalanceDto>>.CreateSuccess(result, "Leave balances retrieved successfully.");
    }
}
