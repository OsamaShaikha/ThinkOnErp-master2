using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class SSCCalculationService : ISSCCalculationService
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IStatutoryRuleService _statutoryRuleService;
    private readonly ILogger<SSCCalculationService> _logger;

    public SSCCalculationService(
        IPolicyRepository policyRepo,
        IStatutoryRuleService statutoryRuleService,
        ILogger<SSCCalculationService> logger)
    {
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _statutoryRuleService = statutoryRuleService ?? throw new ArgumentNullException(nameof(statutoryRuleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SSCCalculationResult> CalculateSSCAsync(
        decimal sscGrossEarnings,
        bool isHighRiskRole,
        long companyId,
        DateTime calculationDate)
    {
        if (sscGrossEarnings <= 0)
        {
            return new SSCCalculationResult(0m, 0m, 0m, 0m, 0m, 0m, "NONE");
        }

        // 1. Try resolving SSC policy from company policies first
        var sscPolicy = await _policyRepo.GetEffectiveSSCPolicyAsync(companyId, calculationDate);

        decimal empRate = sscPolicy?.EmployeeContributionRate ?? 0m;
        decimal emprRate = sscPolicy?.EmployerContributionRate ?? 0m;
        decimal highRiskRate = sscPolicy?.HighRiskSurchargeRate ?? 0m;
        decimal sscCap = sscPolicy?.MonthlyCeilingCap ?? 0m;
        string policyCode = sscPolicy?.Code ?? "JORDAN_SSC_STATUTORY";

        // 2. Fallback to Statutory Rule Service if company policy rate is not configured
        if (empRate <= 0)
        {
            empRate = await _statutoryRuleService.GetEffectiveRateAsync("SSC_EMPLOYEE_RATE", calculationDate);
            if (empRate <= 0) empRate = 0.0750m; // 7.5%
        }

        if (emprRate <= 0)
        {
            emprRate = await _statutoryRuleService.GetEffectiveRateAsync("SSC_EMPLOYER_RATE", calculationDate);
            if (emprRate <= 0) emprRate = 0.1425m; // 14.25%
        }

        if (highRiskRate <= 0 && isHighRiskRole)
        {
            highRiskRate = await _statutoryRuleService.GetEffectiveRateAsync("SSC_HIGH_RISK_SURCHARGE", calculationDate);
            if (highRiskRate <= 0) highRiskRate = 0.0100m; // 1.0%
        }

        if (sscCap <= 0)
        {
            sscCap = await _statutoryRuleService.GetEffectiveValueAsync("SSC_MONTHLY_CAP", calculationDate);
            if (sscCap <= 0) sscCap = 3349.00m;
        }

        // 3. Apply Cap and Compute Deductions
        var sscEligibleSalary = Math.Min(sscGrossEarnings, sscCap);
        var totalEmployerRate = emprRate + (isHighRiskRole ? highRiskRate : 0m);

        var employeeContribution = Math.Round(sscEligibleSalary * empRate, 3);
        var employerContribution = Math.Round(sscEligibleSalary * totalEmployerRate, 3);

        return new SSCCalculationResult(
            SscEligibleSalary: sscEligibleSalary,
            EmployeeContribution: employeeContribution,
            EmployerContribution: employerContribution,
            SscCapApplied: sscCap,
            EmployeeRateApplied: empRate,
            EmployerRateApplied: totalEmployerRate,
            SscPolicyCode: policyCode);
    }
}
