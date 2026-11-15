using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/end-of-service/policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EndOfServicePoliciesController(IEndOfServiceRepository policies) : ControllerBase
{
    [HttpGet]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndOfServicePolicy>>>> GetPolicies(CancellationToken ct)
    {
        var result = await policies.GetPoliciesAsync(GetTenant().CompanyId, ct);
        return Ok(ApiResponse<IReadOnlyList<EndOfServicePolicy>>.CreateSuccess(result));
    }

    [HttpGet("{id:long}")]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> GetPolicy(long id, CancellationToken ct)
    {
        var result = (await policies.GetPoliciesAsync(GetTenant().CompanyId, ct)).SingleOrDefault(p => p.Id == id);
        return result == null
            ? NotFound(ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy not found.", statusCode: StatusCodes.Status404NotFound))
            : Ok(ApiResponse<EndOfServicePolicy>.CreateSuccess(result));
    }

    [HttpPost]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> CreatePolicy(
        [FromBody] CreateEndOfServicePolicyDto request, CancellationToken ct)
    {
        if (!Valid(request, out var error))
            return BadRequest(ApiResponse<EndOfServicePolicy>.CreateFailure(error!, statusCode: StatusCodes.Status400BadRequest));

        var companyId = GetTenant().CompanyId;
        var existing = await policies.GetPoliciesAsync(companyId, ct);
        foreach (var prior in existing.Where(p => p.IsActive && !p.EffectiveTo.HasValue &&
                     (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value.Date < request.EffectiveFrom.Date)))
        {
            prior.EffectiveTo = request.EffectiveFrom.Date.AddDays(-1);
            prior.UpdateUser = User.Identity?.Name ?? "SYSTEM";
            prior.UpdateDate = DateTime.UtcNow;
        }
        if (existing.Any(p => Overlaps(p.EffectiveFrom, p.EffectiveTo, request.EffectiveFrom, request.EffectiveTo)))
            return Conflict(ApiResponse<EndOfServicePolicy>.CreateFailure("An end of service policy overlaps this effective period; choose a non-overlapping period.", statusCode: StatusCodes.Status409Conflict));

        var policy = new EndOfServicePolicy
        {
            CompanyId = companyId, PolicyName = request.PolicyName.Trim(),
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            CalculationBasis = request.CalculationBasis.Trim().ToUpperInvariant(),
            LeaveDailyRateDivisor = request.LeaveDailyRateDivisor,
            NoticePeriodDays = request.NoticePeriodDays,
            ProrateFinalMonthByWorkingDays = request.ProrateFinalMonthByWorkingDays,
            ResignationFactorUnderTwoYears = request.ResignationFactorUnderTwoYears,
            ResignationFactorTwoToFiveYears = request.ResignationFactorTwoToFiveYears,
            ResignationFactorFiveToTenYears = request.ResignationFactorFiveToTenYears,
            ResignationFactorTenYearsAndAbove = request.ResignationFactorTenYearsAndAbove,
            FirstFiveYearsRatePerYear = request.FirstFiveYearsRatePerYear,
            SubsequentYearsRatePerYear = request.SubsequentYearsRatePerYear,
            IsActive = true, CreationUser = User.Identity?.Name ?? "SYSTEM", CreationDate = DateTime.UtcNow
        };
        await policies.AddPolicyAsync(policy, ct);
        await policies.SavePolicyChangesAsync(ct);
        return Ok(ApiResponse<EndOfServicePolicy>.CreateSuccess(policy, "End of service policy version created."));
    }

    [HttpPost("{id:long}/deactivate")]
    [HrPermission("hr-end-of-service", "configure")]
    public async Task<ActionResult<ApiResponse<EndOfServicePolicy>>> Deactivate(long id, CancellationToken ct)
    {
        var policy = (await policies.GetPoliciesAsync(GetTenant().CompanyId, ct)).SingleOrDefault(p => p.Id == id);
        if (policy == null)
            return NotFound(ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy not found.", statusCode: StatusCodes.Status404NotFound));
        if (!policy.IsActive)
            return Conflict(ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy is already inactive.", statusCode: StatusCodes.Status409Conflict));
        var cutoff = policy.EffectiveFrom.HasValue && policy.EffectiveFrom.Value.Date >= DateTime.UtcNow.Date
            ? policy.EffectiveFrom.Value.Date.AddDays(-1)
            : DateTime.UtcNow.Date.AddDays(-1);
        if (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value.Date < cutoff)
            cutoff = policy.EffectiveTo.Value.Date;
        policy.EffectiveTo = cutoff;
        policy.IsActive = false;
        policy.UpdateUser = User.Identity?.Name ?? "SYSTEM";
        policy.UpdateDate = DateTime.UtcNow;
        await policies.SavePolicyChangesAsync(ct);
        return Ok(ApiResponse<EndOfServicePolicy>.CreateSuccess(policy, "End of service policy deactivated."));
    }

    private TenantRequestContext GetTenant()
        => HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) && value is TenantRequestContext tenant && tenant.CompanyId > 0
            ? tenant
            : throw new InvalidOperationException("Trusted tenant context is required.");

    private static bool Valid(CreateEndOfServicePolicyDto request, out string? error)
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
            error = "End of service policy values or effective period are invalid.";
        return error == null;
    }

    private static bool Overlaps(DateTime? aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => (aFrom?.Date ?? DateTime.MinValue.Date) <= (bTo?.Date ?? DateTime.MaxValue.Date) &&
           bFrom.Date <= (aTo?.Date ?? DateTime.MaxValue.Date);
}
