using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record TaxResult(
    decimal MonthlyTaxableGross,
    decimal TotalAnnualExemptions,
    decimal AnnualTaxableNet,
    decimal AnnualGross,
    decimal MonthlyTax,
    decimal MonthlyNationalSolidarityContrib,
    string PolicyCode
);

public interface ITaxCalculationEngine
{
    Task<TaxResult> CalculateIncomeTaxAsync(
        long companyId,
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        DateTime date,
        CancellationToken cancellationToken = default);

    Task<TaxResult> CalculateIncomeTaxAsync(
        long companyId,
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        DateTime date,
        int roundingDecimals,
        CancellationToken cancellationToken = default);

    TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy);

    TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy,
        int roundingDecimals);

    TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy,
        int roundingDecimals,
        int? roundingRules);
}

public sealed class TaxCalculationEngine : ITaxCalculationEngine
{
    private readonly IPolicyRepository _policyRepository;

    public TaxCalculationEngine(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public Task<TaxResult> CalculateIncomeTaxAsync(
        long companyId,
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        DateTime date,
        CancellationToken cancellationToken = default) =>
        CalculateIncomeTaxAsync(companyId, grossSalary, employeeSscContrib, dependentCount, date, 3, cancellationToken);

    public async Task<TaxResult> CalculateIncomeTaxAsync(
        long companyId,
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        DateTime date,
        int roundingDecimals,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyRepository.GetActiveTaxPolicyAsync(companyId, date, cancellationToken);
        return CalculateIncomeTax(grossSalary, employeeSscContrib, dependentCount, policy, roundingDecimals);
    }

    public TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy) =>
        CalculateIncomeTax(grossSalary, employeeSscContrib, dependentCount, policy, 3);

    public TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy,
        int roundingDecimals) => CalculateIncomeTax(grossSalary, employeeSscContrib, dependentCount, policy, roundingDecimals, null);

    public TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy,
        int roundingDecimals,
        int? roundingRules)
    {
        // Company policy driven, with statutory fallback only when unconfigured
        var personalExemptionSelf = policy?.PersonalExemptionSelf ?? 9000.00m;
        var personalExemptionDependent = policy?.PersonalExemptionDependent ?? 1000.00m;
        var nationalThreshold = policy?.NationalContribThreshold ?? 200000.00m;
        var nationalRate = policy?.NationalContribRate ?? 0.010000m;
        var isSscDeductible = policy?.IsSscTaxDeductible ?? true;
        var policyCode = policy?.Code ?? "DEFAULT_TAX";

        // Total annual exemptions = self + (dependents according to company policy max cap)
        int clampedDependents;
        if (policy?.MaxExemptedDependents.HasValue == true)
        {
            clampedDependents = policy.MaxExemptedDependents.Value > 0
                ? Math.Min(dependentCount, policy.MaxExemptedDependents.Value)
                : 0;
        }
        else
        {
            clampedDependents = policy == null ? Math.Min(dependentCount, 3) : dependentCount;
        }
        var totalAnnualExemptions = personalExemptionSelf + (clampedDependents * personalExemptionDependent);
        var monthlyExemptions = totalAnnualExemptions / 12.0m;

        // Base for tax
        decimal taxableBase = grossSalary;
        if (isSscDeductible)
            taxableBase -= employeeSscContrib;

        if (taxableBase <= 0)
        {
            return new TaxResult(0, totalAnnualExemptions, 0, grossSalary * 12.0m, 0, 0, policyCode);
        }

        // Annualized taxable net
        var annualNetTaxable = Math.Max(0m, (taxableBase - monthlyExemptions) * 12.0m);
        if (annualNetTaxable <= 0)
        {
            return new TaxResult(taxableBase, totalAnnualExemptions, 0, grossSalary * 12.0m, 0, 0, policyCode);
        }

        // Brackets evaluation
        var brackets = policy?.Brackets?.OrderBy(b => b.BracketOrder).ToList();
        if (brackets == null || brackets.Count == 0)
        {
            // Standard statutory brackets (5%, 10%, 15%, 20%, 25%)
            brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = 5000, RatePercent = 5.0m },
                new() { BracketOrder = 2, LowerLimit = 5000, UpperLimit = 10000, RatePercent = 10.0m },
                new() { BracketOrder = 3, LowerLimit = 10000, UpperLimit = 15000, RatePercent = 15.0m },
                new() { BracketOrder = 4, LowerLimit = 15000, UpperLimit = 20000, RatePercent = 20.0m },
                new() { BracketOrder = 5, LowerLimit = 20000, UpperLimit = null, RatePercent = 25.0m }
            };
        }

        decimal annualTax = 0.00m;
        foreach (var b in brackets)
        {
            if (annualNetTaxable > b.LowerLimit)
            {
                var taxableInBracket = b.UpperLimit.HasValue
                    ? Math.Min(annualNetTaxable - b.LowerLimit, b.UpperLimit.Value - b.LowerLimit)
                    : annualNetTaxable - b.LowerLimit;

                if (taxableInBracket > 0)
                {
                    annualTax += taxableInBracket * (b.RatePercent / 100.0m);
                }
            }
        }

        var monthlyTaxWithheld = PayrollCurrencyRules.Round(annualTax / 12.0m, roundingDecimals, roundingRules);

        // National solidarity contribution (configurable rate above threshold)
        decimal monthlyNationalContrib = 0.00m;
        if (annualNetTaxable > nationalThreshold)
        {
            var excess = annualNetTaxable - nationalThreshold;
            monthlyNationalContrib = PayrollCurrencyRules.Round((excess * nationalRate) / 12.0m, roundingDecimals, roundingRules);
        }

        return new TaxResult(
            taxableBase,
            totalAnnualExemptions,
            annualNetTaxable,
            grossSalary * 12.0m,
            monthlyTaxWithheld,
            monthlyNationalContrib,
            policyCode
        );
    }
}
