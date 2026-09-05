using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/policies")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class PayrollPoliciesController : ControllerBase
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IOvertimeCalculationService _overtimeService;

    public PayrollPoliciesController(
        IPolicyRepository policyRepo,
        IOvertimeCalculationService overtimeService)
    {
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _overtimeService = overtimeService ?? throw new ArgumentNullException(nameof(overtimeService));
    }

    [HttpGet("overtime-rules")]
    [ProducesResponseType(typeof(List<OvertimeRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OvertimeRuleDto>>> GetOvertimeRules([FromQuery] long companyId = 1)
    {
        var result = await _overtimeService.GetOvertimeRulesAsync(companyId, DateTime.UtcNow);
        return Ok(result);
    }

    [HttpPost("overtime-rules")]
    [ProducesResponseType(typeof(OvertimeRuleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OvertimeRuleDto>> CreateOvertimeRule([FromBody] CreateOvertimeRuleDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _overtimeService.CreateOvertimeRuleAsync(dto, currentUser);
        return Created("", created);
    }

    [HttpGet("proration-policies")]
    [ProducesResponseType(typeof(List<ProrationPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProrationPolicyDto>>> GetProrationPolicies([FromQuery] long companyId = 1)
    {
        var policies = await _policyRepo.GetAllProrationPoliciesAsync(companyId);
        var dtos = policies.Select(p => new ProrationPolicyDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            Code = p.Code,
            NameEn = p.NameEn,
            NameAr = p.NameAr,
            Method = p.Method,
            IsDefault = p.IsDefault,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            IsActive = p.IsActive
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("proration-policies")]
    [ProducesResponseType(typeof(ProrationPolicyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProrationPolicyDto>> CreateProrationPolicy([FromBody] CreateProrationPolicyDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var policy = new ProrationPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            Method = dto.Method.ToUpperInvariant(),
            IsDefault = dto.IsDefault,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true,
            CreationUser = currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _policyRepo.AddProrationPolicyAsync(policy);
        await _policyRepo.SaveChangesAsync();

        return Created("", new ProrationPolicyDto
        {
            Id = policy.Id,
            CompanyId = policy.CompanyId,
            Code = policy.Code,
            NameEn = policy.NameEn,
            NameAr = policy.NameAr,
            Method = policy.Method,
            IsDefault = policy.IsDefault,
            EffectiveFrom = policy.EffectiveFrom,
            EffectiveTo = policy.EffectiveTo,
            IsActive = policy.IsActive
        });
    }

    [HttpGet("deduction-policies")]
    [ProducesResponseType(typeof(List<DeductionPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeductionPolicyDto>>> GetDeductionPolicies([FromQuery] long companyId = 1)
    {
        var policies = await _policyRepo.GetAllDeductionPoliciesAsync(companyId);
        var dtos = policies.Select(p => new DeductionPolicyDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            Code = p.Code,
            NameEn = p.NameEn,
            NameAr = p.NameAr,
            MaxDeductionPercentage = p.MaxDeductionPercentage,
            MinimumNetPayGuarantee = p.MinimumNetPayGuarantee,
            AllowNegativeNetPay = p.AllowNegativeNetPay,
            AutoCapAndCarryForward = p.AutoCapAndCarryForward,
            IsDefault = p.IsDefault,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            IsActive = p.IsActive
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("deduction-policies")]
    [ProducesResponseType(typeof(DeductionPolicyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DeductionPolicyDto>> CreateDeductionPolicy([FromBody] CreateDeductionPolicyDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var policy = new DeductionPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            MaxDeductionPercentage = dto.MaxDeductionPercentage,
            MinimumNetPayGuarantee = dto.MinimumNetPayGuarantee,
            AllowNegativeNetPay = dto.AllowNegativeNetPay,
            AutoCapAndCarryForward = dto.AutoCapAndCarryForward,
            IsDefault = dto.IsDefault,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true,
            CreationUser = currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _policyRepo.AddDeductionPolicyAsync(policy);
        await _policyRepo.SaveChangesAsync();

        return Created("", new DeductionPolicyDto
        {
            Id = policy.Id,
            CompanyId = policy.CompanyId,
            Code = policy.Code,
            NameEn = policy.NameEn,
            NameAr = policy.NameAr,
            MaxDeductionPercentage = policy.MaxDeductionPercentage,
            MinimumNetPayGuarantee = policy.MinimumNetPayGuarantee,
            AllowNegativeNetPay = policy.AllowNegativeNetPay,
            AutoCapAndCarryForward = policy.AutoCapAndCarryForward,
            IsDefault = policy.IsDefault,
            EffectiveFrom = policy.EffectiveFrom,
            EffectiveTo = policy.EffectiveTo,
            IsActive = policy.IsActive
        });
    }
}
