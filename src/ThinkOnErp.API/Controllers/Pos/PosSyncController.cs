using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Features.Pos.Sync.Commands.PushOfflineOrders;
using ThinkOnErp.Application.Features.Pos.Sync.Queries.PullCatalogSync;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Offline-First Synchronization API: Handles delta catalog pulls and idempotent offline order batches with inventory conflict logging.
/// </summary>
[ApiController]
[Route("api/pos/sync")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosSyncController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosSyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Pulls updated catalog delta (Items, Barcodes, Prices, Promotions, Floors/Tables) for offline storage.
    /// </summary>
    [HttpGet("pull")]
    [ProducesResponseType(typeof(ApiResponse<PullCatalogSyncDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PullCatalogSyncDto>>> PullCatalog(
        [FromQuery] long branchId,
        [FromQuery] DateTime? lastSyncTime,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new PullCatalogSyncQuery(branchId, lastSyncTime), ct);
        return Ok(result);
    }

    /// <summary>
    /// Pushes offline order batches from cashier client; handles idempotency and records inventory deficit conflicts.
    /// </summary>
    [HttpPost("push")]
    [ProducesResponseType(typeof(ApiResponse<PushOfflineOrdersResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PushOfflineOrdersResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PushOfflineOrdersResultDto>>> PushOfflineOrders(
        [FromQuery] long branchId,
        [FromBody] List<CreatePosOrderDto> orders,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new PushOfflineOrdersCommand(branchId, orders, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
