using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Settings;
using ThinkOnErp.Application.Services.Settings;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Unified Settings API: Full management for Company-wide defaults and independent Branch-specific settings with hierarchical inheritance.
/// </summary>
[ApiController]
[Route("api/settings")]
[ApiExplorerSettings(GroupName = ApiCategories.System)]
[TenantScoped]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Retrieves company-level global defaults (inherited by all branches unless overridden).
    /// </summary>
    [HttpGet("company")]
    [ProducesResponseType(typeof(ApiResponse<CompanySettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CompanySettingsDto>>> GetCompanySettings(CancellationToken ct)
    {
        var response = await _settingsService.GetCompanySettingsAsync(ct);
        return Ok(response);
    }

    /// <summary>
    /// Updates company-level global default settings.
    /// </summary>
    [HttpPut("company")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CompanySettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompanySettingsDto>>> UpdateCompanySettings(
        [FromBody] UpdateCompanySettingsDto dto,
        CancellationToken ct)
    {
        var username = User.Identity?.Name ?? "system";
        var response = await _settingsService.UpdateCompanySettingsAsync(dto, username, ct);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a comparative list of all branches with their effective settings and customization statuses.
    /// </summary>
    [HttpGet("branches")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchSettingsDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<BranchSettingsDto>>>> GetAllBranchSettings(CancellationToken ct)
    {
        var response = await _settingsService.GetAllBranchSettingsAsync(ct);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves effective settings for a specific branch (including branch-specific overrides and inherited company defaults).
    /// </summary>
    /// <param name="branchId">Target branch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("branches/{branchId:long}")]
    [ProducesResponseType(typeof(ApiResponse<BranchSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BranchSettingsDto>>> GetBranchSettings(
        [FromRoute] long branchId,
        CancellationToken ct)
    {
        var response = await _settingsService.GetBranchSettingsAsync(branchId, ct);
        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }

    /// <summary>
    /// Configures or updates independent operational settings for a specific branch (e.g. cash drawer account, default warehouse, stock deduction mode).
    /// </summary>
    /// <param name="branchId">Target branch identifier.</param>
    /// <param name="dto">Branch settings payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("branches/{branchId:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<BranchSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BranchSettingsDto>>> UpdateBranchSettings(
        [FromRoute] long branchId,
        [FromBody] UpdateBranchSettingsDto dto,
        CancellationToken ct)
    {
        var username = User.Identity?.Name ?? "system";
        var response = await _settingsService.UpdateBranchSettingsAsync(branchId, dto, username, ct);
        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }

    /// <summary>
    /// Resets all customized settings of a branch back to the company default settings.
    /// </summary>
    /// <param name="branchId">Target branch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("branches/{branchId:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> ResetBranchSettings(
        [FromRoute] long branchId,
        CancellationToken ct)
    {
        var response = await _settingsService.ResetBranchSettingsAsync(branchId, ct);
        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }
}
