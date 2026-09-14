using System;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record ProrationResult(
    decimal ProrationFactor,
    decimal TotalBaseDays,
    decimal EligibleDays,
    string MethodUsed,
    string PolicyCode
);

public interface IPayrollProrationService
{
    Task<ProrationResult> CalculateProrationFactorAsync(
        long companyId,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime hireDate,
        DateTime? terminationDate,
        ProrationPolicy? policy = null,
        CancellationToken cancellationToken = default);

    ProrationResult CalculateProrationFactor(
        DateTime periodStart,
        DateTime periodEnd,
        DateTime hireDate,
        DateTime? terminationDate,
        ProrationPolicy? policy = null);
}

public sealed class PayrollProrationService : IPayrollProrationService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IWorkCalendarService _calendarService;

    public PayrollProrationService(IPolicyRepository policyRepository, IWorkCalendarService calendarService)
    {
        _policyRepository = policyRepository;
        _calendarService = calendarService;
    }

    public async Task<ProrationResult> CalculateProrationFactorAsync(
        long companyId,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime hireDate,
        DateTime? terminationDate,
        ProrationPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= await _policyRepository.GetActiveProrationPolicyAsync(companyId, periodStart, cancellationToken);
        var method = policy?.Method ?? "CALENDAR_DAYS";
        var policyCode = policy?.Code ?? "DEFAULT_PRORATION";

        var effectiveEmpStart = hireDate > periodStart ? hireDate.Date : periodStart.Date;
        var effectiveEmpEnd = (terminationDate.HasValue && terminationDate.Value < periodEnd)
            ? terminationDate.Value.Date
            : periodEnd.Date;

        if (effectiveEmpStart > periodEnd || (terminationDate.HasValue && terminationDate.Value < periodStart))
        {
            return new ProrationResult(0.000000m, 0, 0, method, policyCode);
        }

        if (effectiveEmpStart <= periodStart && effectiveEmpEnd >= periodEnd)
        {
            var totalCalendarDays = (periodEnd.Date - periodStart.Date).Days + 1;
            return new ProrationResult(1.000000m, totalCalendarDays, totalCalendarDays, method, policyCode);
        }

        if (method.Equals("WORKING_DAYS", StringComparison.OrdinalIgnoreCase))
        {
            var totalWorkingDays = await _calendarService.CountWorkingDaysAsync(companyId, periodStart, periodEnd, cancellationToken);
            if (totalWorkingDays <= 0) totalWorkingDays = 22;

            var workedDays = await _calendarService.CountWorkingDaysAsync(companyId, effectiveEmpStart, effectiveEmpEnd, cancellationToken);
            var factor = Math.Round((decimal)workedDays / (decimal)totalWorkingDays, 6);
            return new ProrationResult(factor, totalWorkingDays, workedDays, "WORKING_DAYS", policyCode);
        }

        return CalculateProrationFactor(periodStart, periodEnd, hireDate, terminationDate, policy);
    }

    public ProrationResult CalculateProrationFactor(
        DateTime periodStart,
        DateTime periodEnd,
        DateTime hireDate,
        DateTime? terminationDate,
        ProrationPolicy? policy = null)
    {
        var method = policy?.Method ?? "CALENDAR_DAYS";
        var policyCode = policy?.Code ?? "DEFAULT_PRORATION";

        var effectiveEmpStart = hireDate > periodStart ? hireDate.Date : periodStart.Date;
        var effectiveEmpEnd = (terminationDate.HasValue && terminationDate.Value < periodEnd)
            ? terminationDate.Value.Date
            : periodEnd.Date;

        if (effectiveEmpStart > periodEnd || (terminationDate.HasValue && terminationDate.Value < periodStart))
        {
            return new ProrationResult(0.000000m, 0, 0, method, policyCode);
        }

        if (effectiveEmpStart <= periodStart && effectiveEmpEnd >= periodEnd)
        {
            var totalCalendarDays = (periodEnd.Date - periodStart.Date).Days + 1;
            return new ProrationResult(1.000000m, totalCalendarDays, totalCalendarDays, method, policyCode);
        }

        switch (method.ToUpperInvariant())
        {
            case "FIXED_30":
            {
                const decimal baseDays = 30.0m;
                var rawEligibleDays = (effectiveEmpEnd - effectiveEmpStart).Days + 1;
                var eligibleDays = Math.Min((decimal)rawEligibleDays, baseDays);
                var factor = Math.Round(eligibleDays / baseDays, 6);
                return new ProrationResult(factor, baseDays, eligibleDays, "FIXED_30", policyCode);
            }

            case "CALENDAR_DAYS":
            default:
            {
                var totalDays = (decimal)((periodEnd.Date - periodStart.Date).Days + 1);
                var eligibleDays = (decimal)((effectiveEmpEnd - effectiveEmpStart).Days + 1);
                var factor = Math.Round(eligibleDays / totalDays, 6);
                return new ProrationResult(factor, totalDays, eligibleDays, "CALENDAR_DAYS", policyCode);
            }
        }
    }
}
