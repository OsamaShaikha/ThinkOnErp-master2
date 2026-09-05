using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class TaxCalculationEngine : ITaxCalculationEngine
{
    private readonly IPolicyRepository _policyRepo;
    private readonly ILogger<TaxCalculationEngine> _logger;

    public TaxCalculationEngine(IPolicyRepository policyRepo, ILogger<TaxCalculationEngine> logger)
    {
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaxCalculationResult> CalculateTaxAsync(
        decimal monthlyTaxableGross,
        int taxExemptionCount,
        long companyId,
        DateTime calculationDate)
    {
        if (monthlyTaxableGross <= 0)
        {
            return new TaxCalculationResult(0m, 0m, 0m, 0m, "NONE");
        }

        // 1. Resolve effective Tax Policy from Database
        var policy = await _policyRepo.GetEffectiveTaxPolicyAsync(companyId, calculationDate);

        var selfExemption = policy?.PersonalExemptionSelf ?? 9000.0m;
        var dependentExemptionUnit = policy?.PersonalExemptionDependent ?? 9000.0m;
        var nationalThreshold = policy?.NationalContributionThreshold ?? 200000.0m;
        var nationalRate = policy?.NationalContributionRate ?? 0.010m;
        var policyCode = policy?.Code ?? "JORDAN_ISTD_DEFAULT";

        // 2. Compute Annualized Gross & Exemptions
        var annualTaxableGross = monthlyTaxableGross * 12m;

        var totalExemptions = selfExemption;
        if (taxExemptionCount > 0)
        {
            var dependentExemption = Math.Min(dependentExemptionUnit, taxExemptionCount * (dependentExemptionUnit / 3m));
            totalExemptions += dependentExemption;
        }

        var annualTaxableNet = Math.Max(0, annualTaxableGross - totalExemptions);
        if (annualTaxableNet <= 0)
        {
            return new TaxCalculationResult(0m, 0m, 0m, totalExemptions, policyCode);
        }

        // 3. Dynamic Progressive Brackets Calculation
        decimal annualTax = 0m;
        var brackets = policy?.Brackets.OrderBy(b => b.BracketOrder).ToList();

        if (brackets != null && brackets.Count > 0)
        {
            var remaining = annualTaxableNet;
            foreach (var bracket in brackets)
            {
                if (remaining <= 0) break;

                if (bracket.UpperLimit.HasValue)
                {
                    var bracketSpan = bracket.UpperLimit.Value - bracket.LowerLimit;
                    var taxableInBracket = Math.Min(remaining, bracketSpan);
                    annualTax += taxableInBracket * bracket.RatePercent;
                    remaining -= taxableInBracket;
                }
                else
                {
                    // Top bracket with no upper limit
                    annualTax += remaining * bracket.RatePercent;
                    remaining = 0;
                }
            }
        }
        else
        {
            // Standard progressive fallback brackets if DB table has not been seeded with custom brackets
            var remaining = annualTaxableNet;

            var b1 = Math.Min(remaining, 5000m);
            annualTax += b1 * 0.05m;
            remaining -= b1;

            if (remaining > 0)
            {
                var b2 = Math.Min(remaining, 5000m);
                annualTax += b2 * 0.10m;
                remaining -= b2;
            }

            if (remaining > 0)
            {
                var b3 = Math.Min(remaining, 5000m);
                annualTax += b3 * 0.15m;
                remaining -= b3;
            }

            if (remaining > 0)
            {
                var b4 = Math.Min(remaining, 5000m);
                annualTax += b4 * 0.20m;
                remaining -= b4;
            }

            if (remaining > 0)
            {
                var b5 = Math.Min(remaining, 980000m);
                annualTax += b5 * 0.25m;
                remaining -= b5;
            }

            if (remaining > 0)
            {
                annualTax += remaining * 0.30m;
            }
        }

        var monthlyTax = Math.Round(annualTax / 12m, 3);

        // 4. National Contribution Surcharge (e.g. 1% above 200,000 JOD)
        decimal monthlyNationalContrib = 0m;
        if (annualTaxableNet > nationalThreshold && nationalRate > 0)
        {
            var annualSurcharge = (annualTaxableNet - nationalThreshold) * nationalRate;
            monthlyNationalContrib = Math.Round(annualSurcharge / 12m, 3);
        }

        return new TaxCalculationResult(
            MonthlyIncomeTax: monthlyTax,
            MonthlyNationalContribution: monthlyNationalContrib,
            AnnualTaxableNet: annualTaxableNet,
            AnnualExemptionsApplied: totalExemptions,
            TaxPolicyCode: policyCode);
    }
}
