using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.System;
using ThinkOnErp.Application.Services.Localization;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// SuperAdmin controller for managing centralized database-driven validation rules stored in THINKON_ERP.SYS_FIELD_VALIDATION_RULE.
/// </summary>
[ApiController]
[Route("api/superadmin/validation-rules")]
[Authorize]
public class ValidationRulesController : ControllerBase
{
    private readonly OracleDbContext _db;
    private readonly IValidationRuleCacheService _cacheService;
    private readonly ILocalizationService _localizationService;
    private readonly IDynamicValidationEngine _validationEngine;
    private readonly ILogger<ValidationRulesController> _logger;

    public ValidationRulesController(
        OracleDbContext db,
        IValidationRuleCacheService cacheService,
        ILocalizationService localizationService,
        IDynamicValidationEngine validationEngine,
        ILogger<ValidationRulesController> logger)
    {
        _db = db;
        _cacheService = cacheService;
        _localizationService = localizationService;
        _validationEngine = validationEngine;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all dynamic validation rules with optional filters (entity, country, company, active status).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SysFieldValidationRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysFieldValidationRuleDto>>>> GetAll([FromQuery] ValidationRuleFilterDto filter)
    {
        var query = _db.SysFieldValidationRules.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
            query = query.Where(r => r.EntityName.ToLower() == filter.EntityName.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
            query = query.Where(r => r.CountryCode == filter.CountryCode.Trim().ToUpper());

        if (filter.CompanyId.HasValue)
            query = query.Where(r => r.CompanyId == filter.CompanyId.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(r => r.IsActive == filter.IsActive.Value);

        var list = await query.OrderBy(r => r.EntityName).ThenBy(r => r.FieldName).ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<SysFieldValidationRuleDto>>.CreateSuccess(dtos, "Validation rules retrieved successfully."));
    }

    /// <summary>
    /// Retrieves a single validation rule by its ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SysFieldValidationRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SysFieldValidationRuleDto>>> GetById(long id)
    {
        var rule = await _db.SysFieldValidationRules.FindAsync(id);
        if (rule == null)
            return NotFound(ApiResponse<SysFieldValidationRuleDto>.CreateFailure($"Validation rule #{id} not found.", statusCode: StatusCodes.Status404NotFound));

        return Ok(ApiResponse<SysFieldValidationRuleDto>.CreateSuccess(MapToDto(rule), "Validation rule retrieved successfully."));
    }

    /// <summary>
    /// Creates a new dynamic validation rule and immediately refreshes the in-memory rule cache.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SysFieldValidationRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SysFieldValidationRuleDto>>> Create([FromBody] CreateValidationRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EntityName) || string.IsNullOrWhiteSpace(dto.FieldName) || string.IsNullOrWhiteSpace(dto.RuleType))
        {
            return BadRequest(ApiResponse<SysFieldValidationRuleDto>.CreateFailure("EntityName, FieldName, and RuleType are mandatory."));
        }

        var rule = new SysFieldValidationRule
        {
            EntityName = dto.EntityName.Trim(),
            FieldName = dto.FieldName.Trim(),
            RuleType = dto.RuleType.Trim().ToUpperInvariant(),
            RuleValue = dto.RuleValue?.Trim(),
            ErrorCode = string.IsNullOrWhiteSpace(dto.ErrorCode) ? "ERR_VALIDATION_FAILED" : dto.ErrorCode.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? null : dto.CountryCode.Trim().ToUpperInvariant(),
            CompanyId = dto.CompanyId,
            IsActive = 1,
            CreationUser = User.Identity?.Name ?? "system",
            CreationDate = DateTime.UtcNow
        };

        _db.SysFieldValidationRules.Add(rule);
        await _db.SaveChangesAsync();

        // Refresh cache
        await _cacheService.RefreshCacheAsync();

        return CreatedAtAction(nameof(GetById), new { id = rule.Id },
            ApiResponse<SysFieldValidationRuleDto>.CreateSuccess(MapToDto(rule), "Validation rule created and cache updated successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates an existing validation rule (e.g. adjusts regex pattern, range limits, or error code).
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SysFieldValidationRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SysFieldValidationRuleDto>>> Update(long id, [FromBody] UpdateValidationRuleDto dto)
    {
        var rule = await _db.SysFieldValidationRules.FindAsync(id);
        if (rule == null)
            return NotFound(ApiResponse<SysFieldValidationRuleDto>.CreateFailure($"Validation rule #{id} not found.", statusCode: StatusCodes.Status404NotFound));

        if (dto.RuleValue != null) rule.RuleValue = dto.RuleValue.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ErrorCode)) rule.ErrorCode = dto.ErrorCode.Trim();
        if (dto.CountryCode != null) rule.CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? null : dto.CountryCode.Trim().ToUpperInvariant();
        if (dto.CompanyId.HasValue) rule.CompanyId = dto.CompanyId.Value == 0 ? null : dto.CompanyId;
        if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;

        rule.UpdateUser = User.Identity?.Name ?? "system";
        rule.UpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _cacheService.RefreshCacheAsync();

        return Ok(ApiResponse<SysFieldValidationRuleDto>.CreateSuccess(MapToDto(rule), "Validation rule updated and cache refreshed successfully."));
    }

    /// <summary>
    /// Deactivates / deletes a validation rule and invalidates the cache.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
    {
        var rule = await _db.SysFieldValidationRules.FindAsync(id);
        if (rule == null)
            return NotFound(ApiResponse<object>.CreateFailure($"Validation rule #{id} not found.", statusCode: StatusCodes.Status404NotFound));

        _db.SysFieldValidationRules.Remove(rule);
        await _db.SaveChangesAsync();
        await _cacheService.RefreshCacheAsync();

        return Ok(ApiResponse<object>.CreateSuccess(null, $"Validation rule #{id} removed successfully."));
    }

    /// <summary>
    /// Explicitly triggers an immediate cache refresh across validation rules and localized error dictionaries.
    /// </summary>
    [HttpPost("refresh-cache")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RefreshCache()
    {
        await _cacheService.RefreshCacheAsync();
        _localizationService.RefreshCache();
        return Ok(ApiResponse<object>.CreateSuccess(null, "Dynamic validation rules and localization cache refreshed successfully."));
    }

    private static SysFieldValidationRuleDto MapToDto(SysFieldValidationRule r) => new()
    {
        Id = r.Id,
        EntityName = r.EntityName,
        FieldName = r.FieldName,
        RuleType = r.RuleType,
        RuleValue = r.RuleValue,
        ErrorCode = r.ErrorCode,
        CountryCode = r.CountryCode,
        CompanyId = r.CompanyId,
        IsActive = r.IsActive,
        CreationUser = r.CreationUser,
        CreationDate = r.CreationDate,
        UpdateUser = r.UpdateUser,
        UpdateDate = r.UpdateDate
    };
}
