using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.CostCenters;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/cost-centers")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class CostCentersController : ControllerBase
{
    private readonly IGlCostCenterService _costCenterService;

    public CostCentersController(IGlCostCenterService costCenterService)
    {
        _costCenterService = costCenterService ?? throw new ArgumentNullException(nameof(costCenterService));
    }

    /// <summary>
    /// Retrieves all cost centers in a flat list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlCostCenterDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlCostCenterDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _costCenterService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlCostCenterDto>>.CreateSuccess(list, ResponseCodes.CostCentersRetrieved, StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves cost centers formatted as a hierarchical tree.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlCostCenterTreeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GlCostCenterTreeDto>>>> GetTree(CancellationToken cancellationToken)
    {
        var tree = await _costCenterService.GetTreeAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlCostCenterTreeDto>>.CreateSuccess(tree, ResponseCodes.CostCenterTreeRetrieved, StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves details of a specific cost center by code.
    /// </summary>
    [HttpGet("{costCenterCode}")]
    [ProducesResponseType(typeof(ApiResponse<GlCostCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlCostCenterDto>>> GetByCode(string costCenterCode, CancellationToken cancellationToken)
    {
        var center = await _costCenterService.GetByCodeAsync(costCenterCode, cancellationToken);
        if (center == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.CostCenterNotFound, statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<GlCostCenterDto>.CreateSuccess(center, ResponseCodes.DataRetrieved, StatusCodes.Status200OK));
    }

    /// <summary>
    /// Creates a new cost center.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<GlCostCenterDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GlCostCenterDto>>> Create([FromBody] CreateGlCostCenterDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.CreateFailure(ErrorCodes.ValidationError, errors, StatusCodes.Status400BadRequest));
        }

        try
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var created = await _costCenterService.CreateAsync(dto, username, cancellationToken);
            return CreatedAtAction(nameof(GetByCode), new { costCenterCode = created.CostCenterCode }, ApiResponse<GlCostCenterDto>.CreateSuccess(created, ResponseCodes.CostCenterCreated, StatusCodes.Status201Created));
        }
        catch (AccountingException ex)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// Updates an existing cost center by code.
    /// </summary>
    [HttpPut("{costCenterCode}")]
    [ProducesResponseType(typeof(ApiResponse<GlCostCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlCostCenterDto>>> Update(string costCenterCode, [FromBody] UpdateGlCostCenterDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.CreateFailure(ErrorCodes.ValidationError, errors, StatusCodes.Status400BadRequest));
        }

        try
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var updated = await _costCenterService.UpdateAsync(costCenterCode, dto, username, cancellationToken);
            return Ok(ApiResponse<GlCostCenterDto>.CreateSuccess(updated, ResponseCodes.CostCenterUpdated, StatusCodes.Status200OK));
        }
        catch (AccountingNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (AccountingException ex)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
    }

    /// <summary>
    /// Activates or deactivates a cost center by code.
    /// </summary>
    [HttpPatch("{costCenterCode}/status")]
    [ProducesResponseType(typeof(ApiResponse<GlCostCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlCostCenterDto>>> SetStatus(string costCenterCode, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            var username = User.Identity?.Name ?? "SYSTEM";
            var updated = await _costCenterService.SetActiveStatusAsync(costCenterCode, isActive, username, cancellationToken);
            return Ok(ApiResponse<GlCostCenterDto>.CreateSuccess(updated, ResponseCodes.StatusUpdated, StatusCodes.Status200OK));
        }
        catch (AccountingNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.CreateFailure(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
    }
}
