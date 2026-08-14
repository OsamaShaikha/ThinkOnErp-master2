using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes endpoints to view and manage Chart of Accounts level digit structure configurations
/// and generate default Chart of Accounts trees dynamically based on these settings.
/// </summary>
[ApiController]
[Route("api/accounting/gl-account-structure")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class GlAccountStructureController : ControllerBase
{
    private readonly IGlAccountService _service;
    private readonly ILogger<GlAccountStructureController> _logger;

    public GlAccountStructureController(
        IGlAccountService service,
        ILogger<GlAccountStructureController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of active Chart of Accounts level digit structure configurations.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlAccountStructureConfigDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStructureConfigs(CancellationToken cancellationToken)
    {
        var result = await _service.GetStructureConfigsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlAccountStructureConfigDto>>.CreateSuccess(result, "COA level structure configurations retrieved successfully."));
    }

    /// <summary>
    /// Updates the Chart of Accounts level digit structure configurations.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlAccountStructureConfigDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStructureConfigs(
        [FromBody] List<UpdateGlAccountStructureConfigDto> request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.Count == 0)
        {
            return BadRequest(ApiResponse<object>.CreateFailure("Structure configuration payload cannot be empty."));
        }

        var result = await _service.UpdateStructureConfigsAsync(request, cancellationToken);
        _logger.LogInformation("COA level structure configurations updated by user.");

        return Ok(ApiResponse<IReadOnlyList<GlAccountStructureConfigDto>>.CreateSuccess(result, "COA level structure configurations updated successfully."));
    }

    /// <summary>
    /// Previews the default Chart of Accounts tree dynamically generated based on the current level digit configurations.
    /// </summary>
    [HttpGet("preview-default-tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlAccountTreeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewDefaultTree(CancellationToken cancellationToken)
    {
        var result = await _service.GenerateDefaultTreePreviewAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlAccountTreeDto>>.CreateSuccess(result, "Default COA tree preview generated successfully based on level digit settings."));
    }

    /// <summary>
    /// Seeds the default Chart of Accounts tree dynamically into the database based on the current level digit configurations.
    /// </summary>
    [HttpPost("seed-default-tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDefaultTree(
        [FromQuery] long defaultBranchId = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SeedDefaultTreeAsync(defaultBranchId, cancellationToken);
        _logger.LogInformation("Default COA tree seeded into database based on level digit settings for company.");

        return Ok(ApiResponse<IReadOnlyList<GlAccountDto>>.CreateSuccess(result, "Default COA tree successfully generated and seeded into database."));
    }
}
