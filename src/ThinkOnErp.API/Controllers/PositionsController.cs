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
[Route("api/hr/positions")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PositionsController : ControllerBase
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService)
    {
        _positionService = positionService ?? throw new ArgumentNullException(nameof(positionService));
    }

    /// <summary>
    /// Retrieves all positions in a flat list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PositionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<PositionDto>>>> GetAll([FromQuery] string? departmentCode = null, [FromQuery] bool activeOnly = true)
    {
        var list = await _positionService.GetAllAsync(departmentCode, activeOnly);
        return Ok(ApiResponse<List<PositionDto>>.CreateSuccess(list, "Positions retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Generates hierarchical organizational chart tree.
    /// </summary>
    [HttpGet("/api/hr/org-chart")]
    [ProducesResponseType(typeof(ApiResponse<List<OrgChartNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<OrgChartNodeDto>>>> GetOrgChart([FromQuery] long? branchId = null)
    {
        var orgChart = await _positionService.GetOrgChartAsync(branchId);
        return Ok(ApiResponse<List<OrgChartNodeDto>>.CreateSuccess(orgChart, "Organizational chart generated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Retrieves details of a specific position by code.
    /// </summary>
    [HttpGet("{positionCode}")]
    [ProducesResponseType(typeof(ApiResponse<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> GetByCode(string positionCode)
    {
        var pos = await _positionService.GetByCodeAsync(positionCode);
        if (pos == null)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Position ({positionCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<PositionDto>.CreateSuccess(pos, "Position retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Creates a new position.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PositionDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> Create([FromBody] CreatePositionDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _positionService.CreateAsync(dto, username);
        return CreatedAtAction(nameof(GetByCode), new { positionCode = created.PositionCode }, ApiResponse<PositionDto>.CreateSuccess(created, "Position created successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates an existing position.
    /// </summary>
    [HttpPut("{positionCode}")]
    [ProducesResponseType(typeof(ApiResponse<PositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> Update(string positionCode, [FromBody] UpdatePositionDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var updated = await _positionService.UpdateAsync(positionCode, dto, username);
        return Ok(ApiResponse<PositionDto>.CreateSuccess(updated, "Position updated successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes a position.
    /// </summary>
    [HttpDelete("{positionCode}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string positionCode)
    {
        var success = await _positionService.DeleteAsync(positionCode);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Position ({positionCode}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Position deleted successfully.", StatusCodes.Status200OK));
    }
}
