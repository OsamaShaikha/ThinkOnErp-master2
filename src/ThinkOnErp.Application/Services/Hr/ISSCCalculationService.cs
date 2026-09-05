using System;
using System.Threading.Tasks;

namespace ThinkOnErp.Application.Services.Hr;

public record SSCCalculationResult(
    decimal SscEligibleSalary,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    decimal SscCapApplied,
    decimal EmployeeRateApplied,
    decimal EmployerRateApplied,
    string SscPolicyCode);

public interface ISSCCalculationService
{
    Task<SSCCalculationResult> CalculateSSCAsync(
        decimal sscGrossEarnings,
        bool isHighRiskRole,
        long companyId,
        DateTime calculationDate);
}
