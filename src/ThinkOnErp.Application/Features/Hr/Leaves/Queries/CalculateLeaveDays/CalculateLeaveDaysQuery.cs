using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.CalculateLeaveDays;

public sealed record CalculateLeaveDaysQuery(
    string EmployeeCode,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    long CompanyId) : IRequest<ApiResponse<LeaveDaysCalculationDto>>;

public sealed class CalculateLeaveDaysQueryValidator : AbstractValidator<CalculateLeaveDaysQuery>
{
    public CalculateLeaveDaysQueryValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.LeaveTypeCode).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public sealed class CalculateLeaveDaysQueryHandler : IRequestHandler<CalculateLeaveDaysQuery, ApiResponse<LeaveDaysCalculationDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IWorkCalendarService? _calendarService;

    public CalculateLeaveDaysQueryHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IWorkCalendarService? calendarService = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _calendarService = calendarService;
    }

    public async Task<ApiResponse<LeaveDaysCalculationDto>> Handle(CalculateLeaveDaysQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(request.EmployeeCode));
        if (string.IsNullOrWhiteSpace(request.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(request.LeaveTypeCode));
        var start = request.StartDate.Date;
        var end = request.EndDate.Date;
        if (end < start)
            throw new ArgumentException("End date cannot be earlier than start date.", nameof(request.EndDate));

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee '{request.EmployeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(request.LeaveTypeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave type '{request.LeaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{request.LeaveTypeCode}' is inactive.");

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, emp, request.LeaveTypeCode, start, settings, cancellationToken);
        if (end > cycle.End)
            throw new InvalidOperationException("Split this leave calculation at the leave-year boundary before submission.");
        if (leaveType.TracksBalance)
        {
            if (policy == null && await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, request.LeaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (!LeaveCommon.IsApplicableToPolicy(emp, policy, settings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
        }

        var calendarDays = (end - start).Days + 1;

        int workingDays;
        if (_calendarService != null)
        {
            workingDays = await _calendarService.CountWorkingDaysAsync(request.CompanyId, start, end, cancellationToken);
        }
        else
        {
            workingDays = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday)
                    workingDays++;
            }
        }

        decimal available = 0m;
        DateTime? cycleStart = null;
        DateTime? cycleEnd = null;

        if (leaveType.TracksBalance)
        {
            cycleStart = cycle.Start;
            cycleEnd = cycle.End;

            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cancellationToken);
            var companyToday = LeaveCommon.GetLeaveCompanyToday(settings);
            var asOfDate = companyToday < cycle.End ? companyToday : cycle.End;
            var accrued = balance?.AccruedDays ?? LeaveCommon.CalculateAccruedToDate(emp, leaveType, policy, asOfDate, settings);
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken: cancellationToken);
            available = accrued + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m) - pending;
        }

        var hasSufficientBalance = !leaveType.TracksBalance || (available >= workingDays);

        var result = new LeaveDaysCalculationDto(
            request.EmployeeCode,
            request.LeaveTypeCode,
            start,
            end,
            calendarDays,
            workingDays,
            available,
            hasSufficientBalance,
            cycleStart,
            cycleEnd);

        return ApiResponse<LeaveDaysCalculationDto>.CreateSuccess(result, "Leave days calculated successfully.");
    }
}
