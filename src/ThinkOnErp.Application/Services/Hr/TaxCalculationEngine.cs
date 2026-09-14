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

    TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy);
}

public sealed class TaxCalculationEngine : ITaxCalculationEngine
{
    private readonly IPolicyRepository _policyRepository;

    public TaxCalculationEngine(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<TaxResult> CalculateIncomeTaxAsync(
        long companyId,
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyRepository.GetActiveTaxPolicyAsync(companyId, date, cancellationToken);
        return CalculateIncomeTax(grossSalary, employeeSscContrib, dependentCount, policy);
    }

    public TaxResult CalculateIncomeTax(
        decimal grossSalary,
        decimal employeeSscContrib,
        int dependentCount,
        TaxPolicy? policy)
    {
        // Fallback default statutory tax policy if not yet seeded
        var personalExemptionSelf = policy?.PersonalExemptionSelf ?? 9000.00m;
        var personalExemptionDependent = policy?.PersonalExemptionDependent ?? 1000.00m;
        var nationalThreshold = policy?.NationalContribThreshold ?? 200000.00m;
        var nationalRate = policy?.NationalContribRate ?? 0.010000m;
        var isSscDeductible = policy?.IsSscTaxDeductible ?? true;
        var policyCode = policy?.Code ?? "DEFAULT_TAX";

        // Total annual exemptions = self + (dependents, max 3 typically)
        var clampedDependents = Math.Min(dependentCount, 3);
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

        var monthlyTaxWithheld = Math.Round(annualTax / 12.0m, 3);

        // National solidarity contribution (1% above threshold)
        decimal monthlyNationalContrib = 0.00m;
        if (annualNetTaxable > nationalThreshold)
        {
            var excess = annualNetTaxable - nationalThreshold;
            monthlyNationalContrib = Math.Round((excess * nationalRate) / 12.0m, 3);
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
