using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunMonthlyAccrual;

public sealed record RunMonthlyAccrualCommand(MonthlyLeaveAccrualDto Dto, string User) : IRequest<ApiResponse<LeaveAccrualResultDto>>;

public sealed class RunMonthlyAccrualCommandValidator : AbstractValidator<RunMonthlyAccrualCommand>
{
    public RunMonthlyAccrualCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.MonthNo).InclusiveBetween(1, 12);
        RuleFor(x => x.Dto.YearNo).GreaterThan(0);
    }
}

public sealed class RunMonthlyAccrualCommandHandler : IRequestHandler<RunMonthlyAccrualCommand, ApiResponse<LeaveAccrualResultDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public RunMonthlyAccrualCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<LeaveAccrualResultDto>> Handle(RunMonthlyAccrualCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.MonthNo < 1 || dto.MonthNo > 12)
            throw new ArgumentException("Month must be between 1 and 12.", nameof(dto.MonthNo));

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var hasPolicyHistory = await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, leaveTypeCode, cancellationToken);

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (emp != null && emp.IsActive && emp.EmploymentStatus == "ACTIVE")
                employees.Add(emp);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var accrualDate = new DateTime(dto.YearNo, dto.MonthNo, DateTime.DaysInMonth(dto.YearNo, dto.MonthNo));
        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAccrued = 0m;

        foreach (var emp in employees)
        {
            var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, emp, leaveTypeCode, accrualDate, leaveSettings, cancellationToken);
            if (policy is { IsActive: false }) continue;
            if (policy == null && hasPolicyHistory) continue;
            if (policy != null && !string.Equals(policy.AccrualMethod, "MONTHLY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = emp.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
            if (eligibleFrom > accrualDate)
            {
                continue;
            }
            if (!LeaveCommon.IsApplicableToPolicy(emp, policy, leaveSettings.EligibleCitizenshipCountryCode)) continue;

            var completedServiceYears = LeaveCommon.GetCompletedServiceYears(emp.HireDate, accrualDate);
            var annualQuota = LeaveCommon.GetAnnualEntitlement(policy, leaveType, completedServiceYears);

            var earnedToDate = LeaveCommon.CalculateAccruedToDate(emp, leaveType, policy, accrualDate, leaveSettings);
            if (policy == null)
                earnedToDate = LeaveCommon.CalculateLegacyMonthlyEarned(annualQuota, eligibleFrom, accrualDate, cycle.Start);

            var daysToAdd = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = emp.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = earnedToDate,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, leaveSettings.LeaveYearStartMonth, leaveSettings.LeaveYearStartDay, request.User, cancellationToken);

            if (daysToAdd > 0)
            {
                totalAccrued += daysToAdd;
                updatedCount++;
                details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): +{daysToAdd:F2} days (Accrued: {earnedToDate:F2}/{annualQuota:F2})");
            }
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        var result = new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAccrued,
            details
        );

        return ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Monthly leave accrual completed successfully.");
    }
}
