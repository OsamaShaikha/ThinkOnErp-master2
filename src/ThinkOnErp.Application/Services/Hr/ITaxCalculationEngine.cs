using System;
using System.Threading.Tasks;

namespace ThinkOnErp.Application.Services.Hr;

public record TaxCalculationResult(
    decimal MonthlyIncomeTax,
    decimal MonthlyNationalContribution,
    decimal AnnualTaxableNet,
    decimal AnnualExemptionsApplied,
    string TaxPolicyCode);

public interface ITaxCalculationEngine
{
    Task<TaxCalculationResult> CalculateTaxAsync(
        decimal monthlyTaxableGross,
        int taxExemptionCount,
        long companyId,
        DateTime calculationDate);
}
