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
    /// Calculates and returns available time slots for a branch, date, staff member, or table.
    /// </summary>
    [HttpGet("available-slots")]
    [ProducesResponseType(typeof(ApiResponse<AvailableSlotsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AvailableSlotsResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AvailableSlotsResultDto>>> GetAvailableSlots(
        [FromQuery] long branchId,
        [FromQuery] DateTime date,
        [FromQuery] long? staffEmployeeId = null,
        [FromQuery] long? tableId = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] int durationMinutes = 30,
        [FromQuery] int bufferMinutes = 10,
        [FromQuery] string? openingTime = null,
        [FromQuery] string? closingTime = null,
        CancellationToken ct = default)
    {
        TimeSpan? parsedOpen = null;
        if (!string.IsNullOrWhiteSpace(openingTime) && TimeSpan.TryParse(openingTime, out var op))
            parsedOpen = op;

        TimeSpan? parsedClose = null;
        if (!string.IsNullOrWhiteSpace(closingTime) && TimeSpan.TryParse(closingTime, out var cl))
            parsedClose = cl;

        var result = await _reservationService.GetAvailableSlotsAsync(
            branchId, date, staffEmployeeId, tableId, resourceType, durationMinutes, bufferMinutes, parsedOpen, parsedClose, ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves standard resource types for POS reservations across industries (Salon, Restaurant, Sports, Medical).
    /// </summary>
    [HttpGet("resource-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosResourceTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosResourceTypeDto>>>> GetResourceTypes(CancellationToken ct = default)
    {
        var result = await _reservationService.GetResourceTypesAsync(ct);
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
    /// Updates a reservation by assigning/linking a POS order (OrderNumber and/or OrderId).
    /// </summary>
    [HttpPut("{reservationId:long}/order")]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReservationSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationSummaryDto>>> AssignOrderToReservation(
        long reservationId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] AssignReservationOrderDto? dto,
        [FromQuery] long? orderId,
        [FromQuery] string? orderNumber,
        [FromQuery] PosReservationStatus? status,
        CancellationToken ct)
    {
        dto ??= new AssignReservationOrderDto();
        if (!dto.OrderId.HasValue && orderId.HasValue) dto.OrderId = orderId;
        if (string.IsNullOrWhiteSpace(dto.OrderNumber) && !string.IsNullOrWhiteSpace(orderNumber)) dto.OrderNumber = orderNumber;
        if (!dto.Status.HasValue && status.HasValue) dto.Status = status;

        var result = await _reservationService.AssignOrderToReservationAsync(reservationId, dto, ct);
        return result.Success ? Ok(result) : StatusCode(result.StatusCode, result);
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

    /// <summary>
    /// Creates a recurring standing reservation series (e.g., every Sunday for 12 weeks).
    /// </summary>
    [HttpPost("recurring")]
    [ProducesResponseType(typeof(ApiResponse<RecurringReservationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RecurringReservationResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecurringReservationResultDto>>> CreateRecurringReservation(
        [FromBody] CreateRecurringReservationDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _reservationService.CreateRecurringReservationAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Cancels all future active reservations in a recurring series.
    /// </summary>
    [HttpDelete("recurring/{recurrenceGroupId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> CancelRecurringSeries(
        string recurrenceGroupId,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _reservationService.CancelRecurringSeriesAsync(recurrenceGroupId, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
