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

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunYearlyAllocation;

public sealed record RunYearlyAllocationCommand(YearlyLeaveAllocationDto Dto, string User) : IRequest<ApiResponse<LeaveAccrualResultDto>>;

public sealed class RunYearlyAllocationCommandValidator : AbstractValidator<RunYearlyAllocationCommand>
{
    public RunYearlyAllocationCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.YearNo).GreaterThan(0);
    }
}

public sealed class RunYearlyAllocationCommandHandler : IRequestHandler<RunYearlyAllocationCommand, ApiResponse<LeaveAccrualResultDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public RunYearlyAllocationCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<LeaveAccrualResultDto>> Handle(RunYearlyAllocationCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (dto.AsOfDate.HasValue && dto.AsOfDate.Value.Date > LeaveCommon.GetLeaveCompanyToday(leaveSettings))
            throw new ArgumentException("Yearly allocation cannot target a future company-local date.", nameof(dto.AsOfDate));

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

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
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(
                departmentCode: dto.DepartmentCode, status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAllocated = 0m;

        foreach (var emp in employees)
        {
            var (policy, cycle) = dto.AsOfDate.HasValue
                ? await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, emp, leaveTypeCode, dto.AsOfDate.Value.Date, leaveSettings, cancellationToken)
                : await LeaveCommon.ResolveLeavePolicyCycleForLabelAsync(_leaveRepo, emp, leaveTypeCode, dto.YearNo, leaveSettings, cancellationToken);
            if (policy is { IsActive: false }) continue;
            if (policy == null && hasPolicyHistory) continue;
            if (policy != null && !string.Equals(policy.AccrualMethod, "YEARLY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = emp.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
            if (eligibleFrom > cycle.End) continue;
            if (!LeaveCommon.IsApplicableToPolicy(emp, policy, leaveSettings.EligibleCitizenshipCountryCode)) continue;

            var completedServiceYears = LeaveCommon.GetCompletedServiceYears(emp.HireDate, cycle.End);
            var quota = LeaveCommon.GetAnnualEntitlement(policy, leaveType, completedServiceYears);

            if ((dto.ProrateForMidYearHires || policy?.ProrateFirstCycle == true) && eligibleFrom > cycle.Start)
            {
                var eligibleDays = Math.Max(0, (cycle.End - eligibleFrom).Days + 1);
                quota = Math.Round(quota * eligibleDays / ((cycle.End - cycle.Start).Days + 1), 4, MidpointRounding.AwayFromZero);
            }

            var added = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = emp.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = quota,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, leaveSettings.LeaveYearStartMonth, leaveSettings.LeaveYearStartDay, request.User, cancellationToken);

            totalAllocated += added;
            if (added > 0m) updatedCount++;
            details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): Added {added:F2} days; cycle quota {quota:F2} for {cycle.Start:yyyy-MM-dd}..{cycle.End:yyyy-MM-dd}");
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        var result = new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAllocated,
            details
        );

        return ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Yearly leave grant completed successfully.");
    }
}
