using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class PayrollProrationService : IPayrollProrationService
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IWorkCalendarRepository _workCalendarRepo;
    private readonly ILogger<PayrollProrationService> _logger;

    public PayrollProrationService(
        IPolicyRepository policyRepo,
        IWorkCalendarRepository workCalendarRepo,
        ILogger<PayrollProrationService> logger)
    {
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _workCalendarRepo = workCalendarRepo ?? throw new ArgumentNullException(nameof(workCalendarRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProrationResult> CalculateProrationFactorAsync(
        string employeeCode,
        DateTime hireDate,
        DateTime? terminationDate,
        DateTime periodStart,
        DateTime periodEnd,
        long companyId)
    {
        // Resolve company proration policy for the target period
        var policy = await _policyRepo.GetEffectiveProrationPolicyAsync(companyId, periodStart);
        var method = policy?.Method ?? "FIXED_30";
        var policyCode = policy?.Code ?? "DEFAULT_FIXED_30";

        // Effective employment boundaries within this pay period
        var effectiveStart = hireDate.Date > periodStart.Date ? hireDate.Date : periodStart.Date;
        var effectiveEnd = (terminationDate.HasValue && terminationDate.Value.Date < periodEnd.Date)
            ? terminationDate.Value.Date
            : periodEnd.Date;

        // If employee is not active during this period at all
        if (effectiveStart > periodEnd.Date || (terminationDate.HasValue && terminationDate.Value.Date < periodStart.Date))
        {
            return new ProrationResult(0m, 0m, 30m, method, policyCode);
        }

        // Full month active check
        if (effectiveStart == periodStart.Date && effectiveEnd == periodEnd.Date)
        {
            return new ProrationResult(1.0m, 30m, 30m, method, policyCode);
        }

        // Partial month calculation based on configured method
        if (method == "WORKING_DAYS")
        {
            var calendar = await _workCalendarRepo.GetDefaultCalendarAsync(companyId, periodStart);
            var totalWorkingDays = 0;
            var eligibleWorkingDays = 0;

            var cur = periodStart.Date;
            while (cur <= periodEnd.Date)
            {
                var isWork = calendar != null
                    ? calendar.Days.FirstOrDefault(d => d.DayOfWeek == cur.DayOfWeek)?.IsWorkingDay ?? (cur.DayOfWeek != DayOfWeek.Friday && cur.DayOfWeek != DayOfWeek.Saturday)
                    : (cur.DayOfWeek != DayOfWeek.Friday && cur.DayOfWeek != DayOfWeek.Saturday);

                if (isWork)
                {
                    totalWorkingDays++;
                    if (cur >= effectiveStart && cur <= effectiveEnd)
                    {
                        eligibleWorkingDays++;
                    }
                }

                cur = cur.AddDays(1);
            }

            if (totalWorkingDays == 0) totalWorkingDays = 22;
            var factor = Math.Round((decimal)eligibleWorkingDays / totalWorkingDays, 6);
            return new ProrationResult(factor, eligibleWorkingDays, totalWorkingDays, method, policyCode);
        }
        else if (method == "CALENDAR_DAYS")
        {
            var totalCalendarDays = (decimal)(periodEnd.Date - periodStart.Date).TotalDays + 1m;
            var eligibleCalendarDays = (decimal)(effectiveEnd - effectiveStart).TotalDays + 1m;

            var factor = Math.Round(eligibleCalendarDays / totalCalendarDays, 6);
            return new ProrationResult(factor, eligibleCalendarDays, totalCalendarDays, method, policyCode);
        }
        else // FIXED_30 (Standard Commercial Standard)
        {
            var eligibleDays = (decimal)(effectiveEnd - effectiveStart).TotalDays + 1m;
            // Cap at 30 days
            if (eligibleDays > 30m) eligibleDays = 30m;

            var factor = Math.Round(eligibleDays / 30m, 6);
            return new ProrationResult(factor, eligibleDays, 30m, method, policyCode);
        }
    }
}
