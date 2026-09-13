using System;
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
using ThinkOnErp.Application.Features.Pos.Shifts.Commands.AuditShift;
using ThinkOnErp.Application.Features.Pos.Shifts.Commands.BlindCloseShift;
using ThinkOnErp.Application.Features.Pos.Shifts.Commands.OpenShift;
using ThinkOnErp.Application.Features.Pos.Shifts.Commands.RecordCashMovement;
using ThinkOnErp.Application.Features.Pos.Shifts.Queries.GetActiveShift;
using ThinkOnErp.Application.Features.Pos.Shifts.Queries.GetShiftById;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Shift &amp; Till Management: Controls cash register sessions, cash movements, paged history, and blind shift closings.
/// </summary>
[ApiController]
[Route("api/pos/shifts")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosShiftsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPosShiftService _shiftService;

    public PosShiftsController(IMediator mediator, IPosShiftService shiftService)
    {
        _mediator = mediator;
        _shiftService = shiftService;
    }

    /// <summary>
    /// Retrieves a paginated list of cashier shifts with branch, till, cashier, status, and date filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PosShiftSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PosShiftSummaryDto>>>> GetShifts(
        [FromQuery] long branchId,
        [FromQuery] long? tillId,
        [FromQuery] long? cashierUserId,
        [FromQuery] PosShiftStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _shiftService.GetShiftsPagedAsync(branchId, tillId, cashierUserId, status, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Opens a new cashier shift session on a physical till.
    /// </summary>
    [HttpPost("open")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosShiftSummaryDto>>> OpenShift(
        [FromBody] OpenShiftDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new OpenShiftCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Records a mid-shift cash movement (Float In, Cash Drop, Pay Out, Tip Payout).
    /// </summary>
    [HttpPost("cash-movement")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftCashMovementItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftCashMovementItemDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosShiftCashMovementItemDto>>> RecordCashMovement(
        [FromBody] CashMovementDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new RecordCashMovementCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Performs a Blind Close: cashier inputs actual counted cash without seeing expected balance.
    /// </summary>
    [HttpPost("blind-close")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosShiftSummaryDto>>> BlindCloseShift(
        [FromBody] BlindCloseShiftDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new BlindCloseShiftCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Manager audit and final closure approval for blind-closed shifts.
    /// </summary>
    [HttpPost("audit")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosShiftSummaryDto>>> AuditShift(
        [FromBody] AuditShiftDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _mediator.Send(new AuditShiftCommand(dto, username), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves the active shift for the specified branch, till, or cashier.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosShiftSummaryDto>>> GetActiveShift(
        [FromQuery] long branchId,
        [FromQuery] long? tillId,
        [FromQuery] long? cashierUserId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetActiveShiftQuery(branchId, tillId, cashierUserId), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves shift summary and financial reconciliation details by shift ID.
    /// </summary>
    [HttpGet("{shiftId:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosShiftSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosShiftSummaryDto>>> GetShiftById(
        long shiftId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetShiftByIdQuery(shiftId), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
