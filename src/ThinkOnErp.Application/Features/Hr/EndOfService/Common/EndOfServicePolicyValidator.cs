using System;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Common;

internal static class EndOfServicePolicyValidator
{
    public static bool Valid(CreateEndOfServicePolicyDto request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.PolicyName) || request.PolicyName.Length > 200 ||
            request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date ||
            string.IsNullOrWhiteSpace(request.CalculationBasis) ||
            request.CalculationBasis.Trim().ToUpperInvariant() is not ("BASIC_SALARY" or "GROSS_SALARY") ||
            request.LeaveDailyRateDivisor <= 0 || request.NoticePeriodDays < 0 ||
            request.ResignationFactorUnderTwoYears < 0 || request.ResignationFactorTwoToFiveYears < 0 ||
            request.ResignationFactorFiveToTenYears < 0 || request.ResignationFactorTenYearsAndAbove < 0 ||
            request.FirstFiveYearsRatePerYear < 0 || request.SubsequentYearsRatePerYear < 0 ||
            request.LeaveDailyRateDivisor > 999999.99m ||
            request.ResignationFactorUnderTwoYears > 99.9999m || request.ResignationFactorTwoToFiveYears > 99.9999m ||
            request.ResignationFactorFiveToTenYears > 99.9999m || request.ResignationFactorTenYearsAndAbove > 99.9999m ||
            request.FirstFiveYearsRatePerYear > 99.9999m || request.SubsequentYearsRatePerYear > 99.9999m)
        {
            error = "End of service policy values or effective period are invalid.";
        }
        return error == null;
    }

    public static bool Overlaps(DateTime? aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => (aFrom?.Date ?? DateTime.MinValue.Date) <= (bTo?.Date ?? DateTime.MaxValue.Date) &&
           bFrom.Date <= (aTo?.Date ?? DateTime.MaxValue.Date);
}
