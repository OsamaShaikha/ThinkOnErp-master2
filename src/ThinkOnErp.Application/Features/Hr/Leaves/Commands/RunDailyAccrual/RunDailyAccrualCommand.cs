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

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunDailyAccrual;

public sealed record RunDailyAccrualCommand(DailyLeaveAccrualDto Dto, string User) : IRequest<ApiResponse<LeaveAccrualResultDto>>;

public sealed class RunDailyAccrualCommandValidator : AbstractValidator<RunDailyAccrualCommand>
{
    public RunDailyAccrualCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
    }
}

public sealed class RunDailyAccrualCommandHandler : IRequestHandler<RunDailyAccrualCommand, ApiResponse<LeaveAccrualResultDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public RunDailyAccrualCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<LeaveAccrualResultDto>> Handle(RunDailyAccrualCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.AsOfDate == default) throw new ArgumentException("An accrual date is required.", nameof(dto.AsOfDate));

        var asOfDate = dto.AsOfDate.Date;
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (asOfDate > LeaveCommon.GetLeaveCompanyToday(settings))
            throw new ArgumentException("Daily accrual cannot be run for a future company-local date.", nameof(dto.AsOfDate));

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (employee != null && employee.IsActive && employee.EmploymentStatus == "ACTIVE") employees.Add(employee);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(
                departmentCode: dto.DepartmentCode, status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var details = new List<string>();
        var updatedCount = 0;
        decimal totalAccrued = 0m;
        foreach (var employee in employees)
        {
            var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, employee, leaveTypeCode, asOfDate, settings, cancellationToken);
            if (policy == null || !policy.IsActive || !string.Equals(policy.AccrualMethod, "DAILY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = employee.HireDate.Date.AddMonths(policy.MinServiceMonths);
            if (eligibleFrom > asOfDate) continue;
            if (!LeaveCommon.IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode)) continue;

            var annualQuota = LeaveCommon.GetAnnualEntitlement(policy, leaveType, LeaveCommon.GetCompletedServiceYears(employee.HireDate, asOfDate));
            var targetDays = LeaveCommon.CalculateAccruedToDate(employee, leaveType, policy, asOfDate, settings);
            var added = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = employee.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = targetDays,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, settings.LeaveYearStartMonth, settings.LeaveYearStartDay, request.User, cancellationToken);

            if (added > 0m)
            {
                totalAccrued += added;
                updatedCount++;
                details.Add($"{employee.EmployeeCode} ({employee.NameLocal}): +{added:F4} days (target: {targetDays:F4}/{annualQuota:F4} through {asOfDate:yyyy-MM-dd})");
            }
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);
        var result = new LeaveAccrualResultDto(employees.Count, updatedCount, totalAccrued, details);
        return ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Daily leave accrual completed successfully.");
    }
}
