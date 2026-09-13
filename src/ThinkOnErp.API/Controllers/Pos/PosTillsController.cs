using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Tills &amp; Cash Registers API: Manages physical till stations, IP mappings, and float amounts.
/// </summary>
[ApiController]
[Route("api/pos/tills")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosTillsController : ControllerBase
{
    private readonly IPosTillService _tillService;

    public PosTillsController(IPosTillService tillService)
    {
        _tillService = tillService;
    }

    /// <summary>
    /// Retrieves all tills for a branch with optional active-only filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TillDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TillDto>>>> GetTills(
        [FromQuery] long branchId,
        [FromQuery] bool? activeOnly,
        CancellationToken ct)
    {
        var result = await _tillService.GetTillsByBranchAsync(branchId, activeOnly, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a till by its unique identifier.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TillDto>>> GetTillById(
        long id,
        CancellationToken ct)
    {
        var result = await _tillService.GetTillByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new physical till station for a branch.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TillDto>>> CreateTill(
        [FromBody] CreateTillDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tillService.CreateTillAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates till settings, hardware identifier, IP, and default float.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TillDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TillDto>>> UpdateTill(
        long id,
        [FromBody] UpdateTillDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tillService.UpdateTillAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates a physical till station.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTill(
        long id,
        CancellationToken ct)
    {
        var result = await _tillService.DeleteTillAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
