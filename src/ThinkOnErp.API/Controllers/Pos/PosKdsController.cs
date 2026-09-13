using System.Collections.Generic;
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
/// Kitchen Display System (KDS) API: Real-time prep board for kitchen stations, prep status tracking, and order bumping.
/// </summary>
[ApiController]
[Route("api/pos/kds")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosKdsController : ControllerBase
{
    private readonly IPosKdsService _kdsService;

    public PosKdsController(IPosKdsService kdsService)
    {
        _kdsService = kdsService;
    }

    /// <summary>
    /// Retrieves active order tickets for kitchen display stations.
    /// </summary>
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<KdsTicketDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<KdsTicketDto>>>> GetActiveKitchenTickets(
        [FromQuery] long branchId,
        [FromQuery] string? station,
        CancellationToken ct)
    {
        var result = await _kdsService.GetActiveKitchenTicketsAsync(branchId, station, ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the prep status of an individual order line item (Pending, Preparing, Ready, Served).
    /// </summary>
    [HttpPut("line-status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateLineStatus(
        [FromBody] UpdateKdsLineStatusDto dto,
        CancellationToken ct)
    {
        var result = await _kdsService.UpdateLineStatusAsync(dto, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Bumps an entire ticket to Ready status across all station lines.
    /// </summary>
    [HttpPost("{orderId:long}/bump")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> BumpTicket(
        long orderId,
        CancellationToken ct)
    {
        var result = await _kdsService.BumpTicketAsync(orderId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
