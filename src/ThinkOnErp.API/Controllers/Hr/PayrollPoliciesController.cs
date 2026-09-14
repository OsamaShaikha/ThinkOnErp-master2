using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/payroll-policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollPoliciesController : ControllerBase
{
    private readonly IPolicyRepository _policyRepo;
    private readonly ILogger<PayrollPoliciesController> _logger;

    public PayrollPoliciesController(
        IPolicyRepository policyRepo,
        ILogger<PayrollPoliciesController> logger)
    {
        _policyRepo = policyRepo;
        _logger = logger;
    }

    // Proration Policy
    [HttpGet("proration")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProrationPolicy>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProrationPolicy>>>> GetProrationPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetProrationPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProrationPolicy>>.CreateSuccess(result, "Proration policies retrieved."));
    }

    [HttpPost("proration")]
    [ProducesResponseType(typeof(ApiResponse<ProrationPolicy>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProrationPolicy>>> AddProrationPolicy(
        [FromBody] ProrationPolicyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var policy = new ProrationPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            Method = dto.Method,
            IsDefault = dto.IsDefault,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };
        await _policyRepo.AddProrationPolicyAsync(policy, cancellationToken);
        await _policyRepo.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<ProrationPolicy>.CreateSuccess(policy, "Proration policy added."));
    }

    // SSC Policy
    [HttpGet("ssc")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SSCPolicy>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SSCPolicy>>>> GetSSCPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetSSCPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SSCPolicy>>.CreateSuccess(result, "SSC policies retrieved."));
    }

    [HttpPost("ssc")]
    [ProducesResponseType(typeof(ApiResponse<SSCPolicy>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SSCPolicy>>> AddSSCPolicy(
        [FromBody] SSCPolicyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var policy = new SSCPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            EmployeeContribRate = dto.EmployeeContribRate,
            EmployerContribRate = dto.EmployerContribRate,
            HighRiskSurchargeRate = dto.HighRiskSurchargeRate,
            MonthlyCeilingCap = dto.MonthlyCeilingCap,
            MinimumWageFloor = dto.MinimumWageFloor,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };
        await _policyRepo.AddSSCPolicyAsync(policy, cancellationToken);
        await _policyRepo.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<SSCPolicy>.CreateSuccess(policy, "SSC policy added."));
    }

    // Tax Policy
    [HttpGet("tax")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxPolicy>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxPolicy>>>> GetTaxPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetTaxPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxPolicy>>.CreateSuccess(result, "Tax policies retrieved."));
    }

    [HttpPost("tax")]
    [ProducesResponseType(typeof(ApiResponse<TaxPolicy>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaxPolicy>>> AddTaxPolicy(
        [FromBody] TaxPolicyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var policy = new TaxPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            PersonalExemptionSelf = dto.PersonalExemptionSelf,
            PersonalExemptionDependent = dto.PersonalExemptionDependent,
            NationalContribThreshold = dto.NationalContribThreshold,
            NationalContribRate = dto.NationalContribRate,
            IsSscTaxDeductible = dto.IsSscTaxDeductible,
            CalculationFrequency = dto.CalculationFrequency,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Brackets != null)
        {
            foreach (var b in dto.Brackets)
            {
                policy.Brackets.Add(new TaxBracket
                {
                    BracketOrder = b.BracketOrder,
                    LowerLimit = b.LowerLimit,
                    UpperLimit = b.UpperLimit,
                    RatePercent = b.RatePercent,
                    Description = b.Description
                });
            }
        }

        await _policyRepo.AddTaxPolicyAsync(policy, cancellationToken);
        await _policyRepo.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<TaxPolicy>.CreateSuccess(policy, "Tax policy added."));
    }

    // Deduction Policy
    [HttpGet("deductions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DeductionPolicy>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DeductionPolicy>>>> GetDeductionPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetDeductionPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DeductionPolicy>>.CreateSuccess(result, "Deduction policies retrieved."));
    }

    [HttpPost("deductions")]
    [ProducesResponseType(typeof(ApiResponse<DeductionPolicy>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DeductionPolicy>>> AddDeductionPolicy(
        [FromBody] DeductionPolicyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var policy = new DeductionPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            MaxDeductionPercentage = dto.MaxDeductionPercentage,
            MinNetPayGuarantee = dto.MinNetPayGuarantee,
            AutoCapAndCarryForward = dto.AutoCapAndCarryForward,
            AllowNegativeNetPay = dto.AllowNegativeNetPay,
            IsDefault = dto.IsDefault,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };
        await _policyRepo.AddDeductionPolicyAsync(policy, cancellationToken);
        await _policyRepo.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<DeductionPolicy>.CreateSuccess(policy, "Deduction policy added."));
    }
}
