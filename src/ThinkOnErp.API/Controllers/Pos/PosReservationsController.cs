using System;
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
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Universal Reservation API: Bookings for restaurant tables, salon rooms/staff, or clinic appointments with deposit tracking.
/// </summary>
[ApiController]
[Route("api/pos/reservations")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosReservationsController : ControllerBase
{
    private readonly IPosReservationService _reservationService;

    public PosReservationsController(IPosReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// Retrieves a paginated list of reservations with multi-criteria filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<ReservationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<ReservationSummaryDto>>>> GetReservations(
        [FromQuery] long branchId,
        [FromQuery] long? customerId,
        [FromQuery] long? tableId,
        [FromQuery] PosReservationStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _reservationService.GetReservationsPagedAsync(
            branchId, customerId, tableId, status, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single reservation by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationSummaryDto>>> GetReservationById(
        long id,
        CancellationToken ct)
    {
        var result = await _reservationService.GetReservationByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new booking reservation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ReservationSummaryDto>>> CreateReservation(
        [FromBody] CreateReservationDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _reservationService.CreateReservationAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates an existing reservation.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationSummaryDto>>> UpdateReservation(
        long id,
        [FromBody] UpdateReservationDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _reservationService.UpdateReservationAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves all reservations for a specific branch and date.
    /// </summary>
    [HttpGet("day")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReservationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReservationSummaryDto>>>> GetReservationsForDay(
        [FromQuery] long branchId,
        [FromQuery] DateTime date,
        CancellationToken ct)
    {
        var result = await _reservationService.GetReservationsForDayAsync(branchId, date, ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates reservation status (Booked, Confirmed, Seated, Cancelled, NoShow).
    /// </summary>
    [HttpPut("{reservationId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateReservationStatus(
        long reservationId,
        [FromQuery] PosReservationStatus status,
        CancellationToken ct)
    {
        var result = await _reservationService.UpdateReservationStatusAsync(reservationId, status, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deletes a reservation.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteReservation(
        long id,
        CancellationToken ct)
    {
        var result = await _reservationService.DeleteReservationAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
