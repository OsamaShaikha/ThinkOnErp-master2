using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/statutory-rules")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class StatutoryRulesController : ControllerBase
{
    private readonly IStatutoryRuleService _ruleService;

    public StatutoryRulesController(IStatutoryRuleService ruleService)
    {
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
    }

    /// <summary>
    /// Retrieves all statutory rules (SSC, ISTD tax brackets, ceilings, minimum wage).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<StatutoryRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<StatutoryRuleDto>>>> GetAll([FromQuery] string? ruleType = null, [FromQuery] bool activeOnly = true)
    {
        var rules = await _ruleService.GetAllRulesAsync(ruleType, activeOnly);
        return Ok(ApiResponse<List<StatutoryRuleDto>>.CreateSuccess(rules, "Statutory rules retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves a statutory rule by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StatutoryRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StatutoryRuleDto>>> GetById(long id)
    {
        var rule = await _ruleService.GetRuleByIdAsync(id);
        if (rule == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Statutory rule ({id}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<StatutoryRuleDto>.CreateSuccess(rule, "Statutory rule retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Resolves the effective tax brackets active on a specific date.
    /// </summary>
    [HttpGet("tax-brackets")]
    [ProducesResponseType(typeof(ApiResponse<List<StatutoryRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<StatutoryRuleDto>>>> GetEffectiveTaxBrackets([FromQuery] DateTime? effectiveDate = null)
    {
        var date = effectiveDate ?? DateTime.UtcNow;
        var brackets = await _ruleService.GetEffectiveTaxBracketsAsync(date);
        return Ok(ApiResponse<List<StatutoryRuleDto>>.CreateSuccess(brackets, "Effective tax brackets retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Creates a new statutory rule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StatutoryRuleDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<StatutoryRuleDto>>> Create([FromBody] CreateStatutoryRuleDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _ruleService.CreateRuleAsync(dto, username);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<StatutoryRuleDto>.CreateSuccess(created, "Statutory rule created successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates an existing statutory rule.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StatutoryRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StatutoryRuleDto>>> Update(long id, [FromBody] UpdateStatutoryRuleDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _ruleService.UpdateRuleAsync(id, dto, username);
        return Ok(ApiResponse<StatutoryRuleDto>.CreateSuccess(updated, "Statutory rule updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes a statutory rule.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
    {
        var success = await _ruleService.DeleteRuleAsync(id);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Statutory rule ({id}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Statutory rule deleted successfully.", StatusCodes.Status200OK));
    }
}
