using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/assets")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class AssetAssignmentsController : ControllerBase
{
    private readonly IAssetAssignmentService _assetService;

    public AssetAssignmentsController(IAssetAssignmentService assetService)
    {
        _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AssetAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssetAssignmentDto>>> GetAllAssignments(
        [FromQuery] string? employeeCode = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null)
    {
        var list = await _assetService.GetAllAssignmentsAsync(employeeCode, status, category);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AssetAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssetAssignmentDto>> GetAssignmentById(long id)
    {
        var asset = await _assetService.GetAssignmentByIdAsync(id);
        if (asset == null)
        {
            return NotFound(new { message = $"Asset assignment #{id} not found." });
        }
        return Ok(asset);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AssetAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetAssignmentDto>> AssignAsset([FromBody] CreateAssetAssignmentDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _assetService.AssignAssetAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetAssignmentById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/return")]
    [ProducesResponseType(typeof(AssetAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssetAssignmentDto>> ReturnAsset(long id, [FromBody] ReturnAssetDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var updated = await _assetService.ReturnAssetAsync(id, dto, currentUser);
        return Ok(updated);
    }
}
