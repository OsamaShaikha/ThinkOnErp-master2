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

    SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy);
}

public sealed class SSCCalculationService : ISSCCalculationService
{
    private readonly IPolicyRepository _policyRepository;

    public SSCCalculationService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<SSCResult> CalculateSSCAsync(
        long companyId,
        decimal grossSalary,
        bool isHighRiskRole,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyRepository.GetActiveSSCPolicyAsync(companyId, date, cancellationToken);
        return CalculateSSC(grossSalary, isHighRiskRole, policy);
    }

    public SSCResult CalculateSSC(
        decimal grossSalary,
        bool isHighRiskRole,
        SSCPolicy? policy)
    {
        // Standard statutory defaults if not in DB:
        // Employee: 7.5% (0.075000), Employer: 14.25% (0.142500), HighRisk: 1.0% (0.010000), Ceiling: 3617.000 JOD
        var empRate = policy?.EmployeeContribRate ?? 0.075000m;
        var emprRate = policy?.EmployerContribRate ?? 0.142500m;
        var highRiskRate = policy?.HighRiskSurchargeRate ?? 0.010000m;
        var ceiling = policy?.MonthlyCeilingCap ?? 3617.000m;
        var policyCode = policy?.Code ?? "DEFAULT_SSC";

        // Capping at maximum statutory ceiling
        var eligibleSalary = Math.Min(grossSalary, ceiling);
        if (eligibleSalary < 0) eligibleSalary = 0;

        var employeeShare = Math.Round(eligibleSalary * empRate, 3);
        
        var effectiveEmployerRate = emprRate;
        decimal highRiskSurcharge = 0.000m;
        if (isHighRiskRole)
        {
            highRiskSurcharge = Math.Round(eligibleSalary * highRiskRate, 3);
            effectiveEmployerRate += highRiskRate;
        }

        var employerShare = Math.Round(eligibleSalary * emprRate, 3) + highRiskSurcharge;

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
