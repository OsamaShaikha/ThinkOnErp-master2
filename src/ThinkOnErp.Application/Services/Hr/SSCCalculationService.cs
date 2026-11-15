using System;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record SSCResult(
    decimal EligibleSalary,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    decimal HighRiskSurcharge,
    decimal EmployeeRate,
    decimal EmployerRate,
    decimal MonthlyCeilingCap,
    string PolicyCode
);

public interface ISSCCalculationService
{
    Task<SSCResult> CalculateSSCAsync(
        long companyId,
        decimal grossSalary,
        bool isHighRiskRole,
        DateTime date,
        CancellationToken cancellationToken = default);

    Task<SSCResult> CalculateSSCAsync(
        long companyId,
        decimal grossSalary,
        bool isHighRiskRole,
        DateTime date,
        int roundingDecimals,
        CancellationToken cancellationToken = default);

    SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy);

    SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy,
        int roundingDecimals);

    SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy,
        int roundingDecimals,
        int? roundingRules);
}

public sealed class SSCCalculationService : ISSCCalculationService
{
    private readonly IPolicyRepository _policyRepository;

    public SSCCalculationService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public Task<SSCResult> CalculateSSCAsync(
        long companyId,
        decimal grossSalary,
        bool isHighRiskRole,
        DateTime date,
        CancellationToken cancellationToken = default) =>
        CalculateSSCAsync(companyId, grossSalary, isHighRiskRole, date, 3, cancellationToken);

    public async Task<SSCResult> CalculateSSCAsync(
        long companyId,
        decimal grossSalary,
        bool isHighRiskRole,
        DateTime date,
        int roundingDecimals,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyRepository.GetActiveSSCPolicyAsync(companyId, date, cancellationToken);
        return CalculateSSC(grossSalary, isHighRiskRole, policy, roundingDecimals);
    }

    public SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy) =>
        CalculateSSC(grossSalary, isHighRiskRole, policy, 3);

    public SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy,
        int roundingDecimals) => CalculateSSC(grossSalary, isHighRiskRole, policy, roundingDecimals, null);

    public SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy,
        int roundingDecimals,
        int? roundingRules)
    {
        // Company policy driven, with statutory fallback only when policy unconfigured
        var empRate = policy?.EmployeeContribRate ?? 0.075000m;
        var emprRate = policy?.EmployerContribRate ?? 0.142500m;
        var highRiskRate = policy?.HighRiskSurchargeRate ?? 0.010000m;
        var ceiling = policy != null
            ? (policy.MonthlyCeilingCap > 0 ? policy.MonthlyCeilingCap : decimal.MaxValue)
            : 3617.000m;
        var policyCode = policy?.Code ?? "DEFAULT_SSC";

        // Capping at configured maximum ceiling (or uncapped if ceiling <= 0 / MaxValue)
        var eligibleSalary = Math.Min(grossSalary, ceiling);
        if (eligibleSalary < 0) eligibleSalary = 0;

        var employeeShare = PayrollCurrencyRules.Round(eligibleSalary * empRate, roundingDecimals, roundingRules);
        
        var effectiveEmployerRate = emprRate;
        decimal highRiskSurcharge = 0.000m;
        if (isHighRiskRole)
        {
            highRiskSurcharge = PayrollCurrencyRules.Round(eligibleSalary * highRiskRate, roundingDecimals, roundingRules);
            effectiveEmployerRate += highRiskRate;
        }

        var employerShare = PayrollCurrencyRules.Round(eligibleSalary * emprRate, roundingDecimals, roundingRules) + highRiskSurcharge;

        return new SSCResult(
            eligibleSalary,
            employeeShare,
            employerShare,
            highRiskSurcharge,
            empRate,
            effectiveEmployerRate,
            ceiling,
            policyCode);
    }
}
