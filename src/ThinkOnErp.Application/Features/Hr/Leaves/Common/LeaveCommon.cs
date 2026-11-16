using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Common;

internal static class LeaveCommon
{
    public static LeaveRequestDto MapLeaveRequest(LeaveRequest request, string? employeeName = null, string? leaveTypeName = null)
    {
        return new LeaveRequestDto(
            request.Id,
            request.EmployeeCode,
            employeeName ?? request.Employee?.NameLocal ?? request.EmployeeCode,
            request.LeaveTypeCode,
            leaveTypeName ?? request.LeaveType?.NameLocal ?? request.LeaveTypeCode,
            request.StartDate,
            request.EndDate,
            request.DaysRequested,
            request.Status,
            request.Reason,
            request.ApprovedBy,
            request.ApprovalDate,
            request.RejectionReason,
            request.CreationDate
        )
        {
            CancelledBy = request.CancelledBy,
            CancellationDate = request.CancellationDate,
            CancellationReason = request.CancellationReason
        };
    }

    public static LeaveBalanceDto FormatBalance(LeaveBalanceDto balance, HrLeaveSettings settings)
    {
        if (settings.BalanceDisplayFormat != "DAYS_HOURS_MINUTES")
            return balance;

        var hours = settings.StandardWorkdayHours.GetValueOrDefault(8m);
        return balance with
        {
            DisplayFormat = "DAYS_HOURS_MINUTES",
            AccruedDisplay = FormatDaysAsDhm(balance.AccruedDays, hours),
            UsedDisplay = FormatDaysAsDhm(balance.UsedDays, hours),
            CarriedForwardDisplay = FormatDaysAsDhm(balance.CarriedForwardDays, hours),
            RemainingDisplay = FormatDaysAsDhm(balance.RemainingDays, hours),
            PendingDisplay = FormatDaysAsDhm(balance.PendingDays, hours),
            AvailableDisplay = FormatDaysAsDhm(balance.AvailableDays, hours)
        };
    }

    public static string FormatDaysAsDhm(decimal days, decimal hoursPerWorkday)
    {
        var totalMinutes = (int)Math.Round(days * hoursPerWorkday * 60m, MidpointRounding.AwayFromZero);
        var sign = totalMinutes < 0 ? "-" : "";
        totalMinutes = Math.Abs(totalMinutes);

        var minutesPerDay = (int)Math.Round(hoursPerWorkday * 60m, MidpointRounding.AwayFromZero);
        if (minutesPerDay <= 0) minutesPerDay = 480;

        var d = totalMinutes / minutesPerDay;
        var rem = totalMinutes % minutesPerDay;
        var h = rem / 60;
        var m = rem % 60;

        return $"{sign}{d}d {h}h {m}m";
    }

    public static LeaveCashoutSettlementDto MapLeaveCashoutSettlement(LeaveCashoutSettlement settlement)
    {
        return new LeaveCashoutSettlementDto(
            settlement.Id,
            settlement.SettlementKey,
            settlement.LeaveYearCloseId,
            settlement.PayPeriod,
            settlement.Lines.Select(l => l.CurrencyCode).Distinct(StringComparer.Ordinal).SingleOrDefault() ?? string.Empty,
            settlement.WageBasisCode,
            settlement.BasisComponentCode,
            settlement.DayDivisor,
            settlement.PayComponentCode,
            settlement.Status,
            settlement.CreationUser,
            DateTime.SpecifyKind(settlement.CreationDate, DateTimeKind.Utc),
            settlement.PostedBy,
            settlement.PostedDate.HasValue ? DateTime.SpecifyKind(settlement.PostedDate.Value, DateTimeKind.Utc) : null,
            settlement.ReversedBy,
            settlement.ReversedDate.HasValue ? DateTime.SpecifyKind(settlement.ReversedDate.Value, DateTimeKind.Utc) : null,
            settlement.ReversalReason,
            settlement.Lines.Sum(l => l.CashoutDays),
            settlement.Lines.Sum(l => l.CashoutAmount),
            settlement.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal)
                .Select(l => new LeaveCashoutSettlementLineDto(
                    l.LeaveYearCloseLineId,
                    l.EmployeeCode,
                    l.LeaveTypeCode,
                    l.CashoutDays,
                    l.SalaryStructureId,
                    DateTime.SpecifyKind(l.SalaryEffectiveFrom, DateTimeKind.Utc),
                    l.WageBasisAmount,
                    l.DayDivisor,
                    l.CashoutAmount,
                    l.CurrencyCode,
                    l.BasisSnapshotJson,
                    l.PayrollAdjustmentId
                )).ToList()
        );
    }

    public static LeaveYearCloseDto MapLeaveYearClose(LeaveYearClose close)
    {
        return new LeaveYearCloseDto(
            close.Id,
            close.FromYear,
            close.ToYear,
            close.LeaveTypeCode,
            close.Status,
            close.CreationUser,
            DateTime.SpecifyKind(close.CreationDate, DateTimeKind.Utc),
            close.PostedBy,
            close.PostedDate.HasValue ? DateTime.SpecifyKind(close.PostedDate.Value, DateTimeKind.Utc) : null,
            close.UnpostedBy,
            close.UnpostedDate.HasValue ? DateTime.SpecifyKind(close.UnpostedDate.Value, DateTimeKind.Utc) : null,
            close.Lines.Sum(l => l.UnusedDays),
            close.Lines.Sum(l => l.CarriedDays),
            close.Lines.Sum(l => l.ExpiredDays),
            close.Lines.Sum(l => l.CashoutDays),
            close.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal)
                .Select(l => new LeaveYearCloseLineDto(
                    l.EmployeeCode,
                    l.LeaveTypeCode,
                    l.SourceYear,
                    l.TargetYear,
                    DateTime.SpecifyKind(l.SourceCycleStartDate ?? new DateTime(l.SourceYear, 1, 1), DateTimeKind.Utc),
                    DateTime.SpecifyKind(l.SourceCycleEndDate ?? new DateTime(l.SourceYear, 12, 31), DateTimeKind.Utc),
                    DateTime.SpecifyKind(l.TargetCycleStartDate, DateTimeKind.Utc),
                    DateTime.SpecifyKind(l.TargetCycleEndDate, DateTimeKind.Utc),
                    l.UnusedDays,
                    l.CarriedDays,
                    l.ExpiredDays,
                    l.CashoutDays
                )).ToList()
        );
    }

    public static LeaveBalanceMigrationDto MapLeaveBalanceMigration(LeaveBalanceMigration migration)
    {
        return new LeaveBalanceMigrationDto(
            migration.Id,
            migration.MigrationKey,
            DateTime.SpecifyKind(migration.EffectiveDate, DateTimeKind.Utc),
            migration.SourceYearStartMonth,
            migration.SourceYearStartDay,
            migration.TargetYearStartMonth,
            migration.TargetYearStartDay,
            migration.Status,
            migration.CreationUser,
            DateTime.SpecifyKind(migration.CreationDate, DateTimeKind.Utc),
            migration.PostedBy,
            migration.PostedDate.HasValue ? DateTime.SpecifyKind(migration.PostedDate.Value, DateTimeKind.Utc) : null,
            migration.ReversedBy,
            migration.ReversedDate.HasValue ? DateTime.SpecifyKind(migration.ReversedDate.Value, DateTimeKind.Utc) : null,
            migration.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal)
                .ThenBy(l => l.SourceCycleStartDate).ThenBy(l => l.TargetCycleStartDate)
                .Select(l => new LeaveBalanceMigrationLineDto(
                    l.SourceBalanceId,
                    l.TargetBalanceId,
                    l.EmployeeCode,
                    l.LeaveTypeCode,
                    DateTime.SpecifyKind(l.SourceCycleStartDate, DateTimeKind.Utc),
                    DateTime.SpecifyKind(l.SourceCycleEndDate, DateTimeKind.Utc),
                    l.SourceAccruedDays,
                    l.SourceUsedDays,
                    l.SourceCarriedDays,
                    DateTime.SpecifyKind(l.TargetCycleStartDate, DateTimeKind.Utc),
                    DateTime.SpecifyKind(l.TargetCycleEndDate, DateTimeKind.Utc),
                    l.AllocatedAccruedDays,
                    l.AllocatedUsedDays,
                    l.AllocatedCarriedDays
                )).ToList()
        );
    }

    public static DateTime GetLeaveCompanyToday(HrLeaveSettings settings)
    {
        var utcNow = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(settings.AccrualTimeZoneId)) return utcNow.Date;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.AccrualTimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone).Date;
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException("The configured leave accrual time zone is not available.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException("The configured leave accrual time zone is invalid.", ex);
        }
    }

    public static decimal CalculateAccruedToDate(Employee employee, LeaveType leaveType, LeavePolicy? policy, DateTime asOfDate, HrLeaveSettings settings)
    {
        if (policy is { IsActive: false }) return 0m;
        if (!IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode)) return 0m;
        var eligibleFrom = employee.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
        if (eligibleFrom > asOfDate.Date)
            return 0m;

        var completedServiceYears = GetCompletedServiceYears(employee.HireDate, asOfDate);
        var annualQuota = GetAnnualEntitlement(policy, leaveType, completedServiceYears);
        var method = policy?.AccrualMethod?.ToUpperInvariant() ?? "YEARLY";

        if (method == "YEARLY")
        {
            if (policy?.ProrateFirstCycle != true)
                return annualQuota;

            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            if (eligibleFrom <= cycle.Start)
                return annualQuota;
            var effectiveStart = eligibleFrom;
            var earnedDays = Math.Max(0, (asOfDate.Date - effectiveStart).Days + 1);
            return Math.Round(annualQuota * earnedDays / cycle.Days, 4, MidpointRounding.AwayFromZero);
        }

        if (method == "DAILY")
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var effectiveStart = eligibleFrom > cycle.Start ? eligibleFrom : cycle.Start;
            var earnedDays = Math.Max(0, (asOfDate.Date - effectiveStart).Days + 1);
            return Math.Round(annualQuota * earnedDays / cycle.Days, 4, MidpointRounding.AwayFromZero);
        }

        if (method == "MONTHLY")
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var monthStart = new DateTime(eligibleFrom.Year, eligibleFrom.Month, 1);
            var monthCursor = monthStart < cycle.Start ? new DateTime(cycle.Start.Year, cycle.Start.Month, 1) : monthStart;
            decimal earned = 0m;
            while (monthCursor <= asOfDate.Date)
            {
                var monthEnd = new DateTime(monthCursor.Year, monthCursor.Month, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
                var effectiveStart = eligibleFrom > monthCursor ? eligibleFrom : monthCursor;
                if (cycle.Start > effectiveStart) effectiveStart = cycle.Start;
                var effectiveEnd = asOfDate.Date < monthEnd ? asOfDate.Date : monthEnd;
                if (effectiveEnd >= effectiveStart)
                    earned += (annualQuota / 12m) * ((effectiveEnd - effectiveStart).Days + 1) / DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                monthCursor = monthCursor.AddMonths(1);
            }
            return Math.Round(earned, 4, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    public static (DateTime Start, DateTime End, int Days) GetAccrualCycle(DateTime hireDate, DateTime asOfDate, string? cycleBasis, HrLeaveSettings settings)
    {
        if (string.Equals(cycleBasis, "EMPLOYMENT_ANNIVERSARY", StringComparison.OrdinalIgnoreCase))
        {
            var anniversary = new DateTime(asOfDate.Year, hireDate.Month, Math.Min(hireDate.Day, DateTime.DaysInMonth(asOfDate.Year, hireDate.Month)));
            var start = anniversary <= asOfDate ? anniversary : anniversary.AddYears(-1);
            var end = start.AddYears(1).AddDays(-1);
            return (start, end, (end - start).Days + 1);
        }

        var companyCycle = GetAccrualCycleForLabel(hireDate, cycleBasis, asOfDate.Year, settings);
        if (asOfDate < companyCycle.Start)
            companyCycle = GetAccrualCycleForLabel(hireDate, cycleBasis, asOfDate.Year - 1, settings);
        return (companyCycle.Start, companyCycle.End, (companyCycle.End - companyCycle.Start).Days + 1);
    }

    public static (DateTime Start, DateTime End, int Days) GetAccrualCycleForLabel(DateTime hireDate, string? cycleBasis, int cycleYear, HrLeaveSettings settings)
    {
        DateTime start;
        if (string.Equals(cycleBasis, "EMPLOYMENT_ANNIVERSARY", StringComparison.OrdinalIgnoreCase))
            start = new DateTime(cycleYear, hireDate.Month, Math.Min(hireDate.Day, DateTime.DaysInMonth(cycleYear, hireDate.Month)));
        else
            start = new DateTime(cycleYear, settings.LeaveYearStartMonth, Math.Min(settings.LeaveYearStartDay, DateTime.DaysInMonth(cycleYear, settings.LeaveYearStartMonth)));
        var end = start.AddYears(1).AddDays(-1);
        return (start, end, (end - start).Days + 1);
    }

    public static async Task<(LeavePolicy? Policy, (DateTime Start, DateTime End, int Days) Cycle)> ResolveLeavePolicyCycleAsync(
        ILeaveRepository leaveRepo, Employee employee, string leaveTypeCode, DateTime asOfDate, HrLeaveSettings settings, CancellationToken cancellationToken)
    {
        var policy = await leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, asOfDate.Date);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var cyclePolicy = await leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, cycle.Start);
            if (cyclePolicy?.Id == policy?.Id) return (cyclePolicy, cycle);
            policy = cyclePolicy;
        }
        throw new InvalidOperationException($"Could not resolve a stable leave policy cycle for '{leaveTypeCode}' and employee '{employee.EmployeeCode}'.");
    }

    public static async Task<(LeavePolicy? Policy, (DateTime Start, DateTime End, int Days) Cycle)> ResolveLeavePolicyCycleForLabelAsync(
        ILeaveRepository leaveRepo, Employee employee, string leaveTypeCode, int cycleYear, HrLeaveSettings settings, CancellationToken cancellationToken)
    {
        var candidate = GetAccrualCycleForLabel(employee.HireDate.Date, null, cycleYear, settings);
        var policy = await leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, candidate.Start);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var cycle = GetAccrualCycleForLabel(employee.HireDate.Date, policy?.CycleBasis, cycleYear, settings);
            var cyclePolicy = await leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, cycle.Start);
            if (cyclePolicy?.Id == policy?.Id) return (cyclePolicy, cycle);
            policy = cyclePolicy;
        }
        throw new InvalidOperationException($"Could not resolve a stable leave policy cycle for '{leaveTypeCode}' and employee '{employee.EmployeeCode}'.");
    }

    public static decimal CalculateLegacyMonthlyEarned(decimal annualQuota, DateTime eligibleFrom, DateTime asOfDate, DateTime cycleStart)
    {
        var firstEligibleMonth = new DateTime(eligibleFrom.Year, eligibleFrom.Month, 1);
        var firstCycleMonth = new DateTime(cycleStart.Year, cycleStart.Month, 1);
        var monthCursor = firstEligibleMonth < firstCycleMonth ? firstCycleMonth : firstEligibleMonth;
        decimal earned = 0m;
        while (monthCursor <= asOfDate.Date)
        {
            var monthEnd = new DateTime(monthCursor.Year, monthCursor.Month, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
            var effectiveStart = eligibleFrom > monthCursor ? eligibleFrom : monthCursor;
            if (cycleStart > effectiveStart) effectiveStart = cycleStart;
            var effectiveEnd = asOfDate.Date < monthEnd ? asOfDate.Date : monthEnd;
            if (effectiveEnd >= effectiveStart)
                earned += (annualQuota / 12m) * ((effectiveEnd - effectiveStart).Days + 1) / DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
            monthCursor = monthCursor.AddMonths(1);
        }
        return Math.Round(earned, 4, MidpointRounding.AwayFromZero);
    }

    public static int GetCompletedServiceYears(DateTime hireDate, DateTime asOfDate)
    {
        var hire = hireDate.Date;
        var asOf = asOfDate.Date;
        if (asOf < hire) return 0;
        var years = asOf.Year - hire.Year;
        var anniversaryDay = Math.Min(hire.Day, DateTime.DaysInMonth(asOf.Year, hire.Month));
        var anniversary = new DateTime(asOf.Year, hire.Month, anniversaryDay);
        return Math.Max(0, asOf < anniversary ? years - 1 : years);
    }

    public static bool IsApplicableToPolicy(Employee employee, LeavePolicy? policy, string? eligibleCitizenshipCountryCode)
    {
        if (policy == null) return true;
        if (!policy.IsActive) return false;
        return policy.ApplicableTo.Trim().ToUpperInvariant() switch
        {
            "ALL" => true,
            "GENDER_F" => string.Equals(employee.Gender?.Trim(), "F", StringComparison.OrdinalIgnoreCase),
            "GENDER_M" => string.Equals(employee.Gender?.Trim(), "M", StringComparison.OrdinalIgnoreCase),
            "CITIZENS" => !string.IsNullOrWhiteSpace(eligibleCitizenshipCountryCode) &&
                !string.IsNullOrWhiteSpace(employee.CitizenshipCountryCode) &&
                string.Equals(employee.CitizenshipCountryCode.Trim(), eligibleCitizenshipCountryCode.Trim(), StringComparison.OrdinalIgnoreCase),
            var value => throw new InvalidOperationException($"Unknown leave policy applicability '{value}'.")
        };
    }

    public static decimal GetAnnualEntitlement(LeavePolicy? policy, LeaveType leaveType, int completedServiceYears)
    {
        if (policy?.AnnualEntitlementDays is > 0m)
            return policy.AnnualEntitlementDays.Value;

        var thresholdYears = policy?.Tier1YearsThreshold ?? 5;
        return completedServiceYears >= thresholdYears
            ? policy?.Tier2Days ?? 21m
            : policy?.Tier1Days ?? leaveType.MaxDaysPerYear;
    }

    public static string NormalizeYearEndExcessAction(string? action)
    {
        var normalized = string.IsNullOrWhiteSpace(action) ? "EXPIRE" : action.Trim().ToUpperInvariant();
        if (normalized is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT"))
            throw new ArgumentException("Year-end excess action must be EXPIRE, REQUIRE_USE, or CASHOUT.", nameof(action));
        return normalized;
    }

    public static async Task<decimal> CalculateRequestedWorkingDaysAsync(
        DateTime start, DateTime end, long? companyId, IWorkCalendarService? calendarService, CancellationToken cancellationToken)
    {
        if (calendarService != null)
        {
            if (!companyId.HasValue)
                throw new InvalidOperationException("Tenant company context is required to calculate requested leave days.");
            return await calendarService.CountWorkingDaysAsync(companyId.Value, start, end, cancellationToken);
        }

        var workingDays = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Friday && day.DayOfWeek != DayOfWeek.Saturday)
                workingDays++;
        }

        return workingDays;
    }

    public static async Task<bool> HasLeavePolicyHistoryAsync(ILeaveRepository leaveRepo, string leaveTypeCode, CancellationToken cancellationToken)
    {
        var versions = await leaveRepo.GetLeavePolicyVersionsAsync(leaveTypeCode, cancellationToken);
        return versions?.Count > 0;
    }

    public static async Task<LeaveBalanceDto> FormatManualMovementBalanceAsync(
        ILeaveRepository leaveRepo, LeaveBalanceMovement movement, CancellationToken cancellationToken)
    {
        var leaveType = await leaveRepo.GetLeaveTypeByCodeAsync(movement.LeaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{movement.LeaveTypeCode}' no longer exists.");
        var sourceBalance = await leaveRepo.GetLeaveBalanceByCycleAsync(movement.EmployeeCode, movement.LeaveTypeCode,
            movement.CycleStartDate, cancellationToken)
            ?? throw new InvalidOperationException("The original leave balance no longer exists.");
        var cycleEnd = sourceBalance.CycleEndDate ?? new DateTime(sourceBalance.YearNo, 12, 31);
        var pending = await leaveRepo.GetPendingLeaveDaysAsync(movement.EmployeeCode, movement.LeaveTypeCode,
            movement.CycleStartDate, cycleEnd, cancellationToken: cancellationToken);
        var remaining = movement.AccruedAfter + movement.CarriedAfter - movement.UsedAfter;
        var balance = new LeaveBalanceDto(movement.EmployeeCode, movement.LeaveTypeCode, leaveType.NameLocal,
            movement.CycleStartDate.Year, movement.AccruedAfter, movement.UsedAfter, movement.CarriedAfter, remaining)
        { PendingDays = pending, AvailableDays = remaining - pending };
        return FormatBalance(balance, await leaveRepo.GetHrLeaveSettingsAsync(cancellationToken));
    }
}
