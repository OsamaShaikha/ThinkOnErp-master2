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
/// Hospitality Floor and Table Management API: Visual layout design, floor and table CRUD, and active order transfers.
/// </summary>
[ApiController]
[Route("api/pos/tables")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosTablesController : ControllerBase
{
    private readonly IPosTableService _tableService;

    public PosTablesController(IPosTableService tableService)
    {
        _tableService = tableService;
    }

    // =================== FLOORS ===================

    /// <summary>
    /// Retrieves all floors and their interactive table layouts for a branch.
    /// </summary>
    [HttpGet("floors")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FloorLayoutDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FloorLayoutDto>>>> GetFloorLayout(
        [FromQuery] long branchId,
        CancellationToken ct)
    {
        var result = await _tableService.GetFloorLayoutAsync(branchId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all floor definitions for a branch.
    /// </summary>
    [HttpGet("floors/list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FloorDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FloorDto>>>> GetFloors(
        [FromQuery] long branchId,
        CancellationToken ct)
    {
        var result = await _tableService.GetFloorsAsync(branchId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a dining floor by ID.
    /// </summary>
    [HttpGet("floors/{floorId:long}")]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> GetFloorById(
        long floorId,
        CancellationToken ct)
    {
        var result = await _tableService.GetFloorByIdAsync(floorId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new dining floor zone.
    /// </summary>
    [HttpPost("floors")]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> CreateFloor(
        [FromBody] CreateFloorDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tableService.CreateFloorAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates dining floor details and sort order.
    /// </summary>
    [HttpPut("floors/{floorId:long}")]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FloorDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> UpdateFloor(
        long floorId,
        [FromBody] UpdateFloorDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tableService.UpdateFloorAsync(floorId, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates a dining floor.
    /// </summary>
    [HttpDelete("floors/{floorId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteFloor(
        long floorId,
        CancellationToken ct)
    {
        var result = await _tableService.DeleteFloorAsync(floorId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // =================== TABLES ===================

    /// <summary>
    /// Retrieves all tables belonging to a specific floor.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TableDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TableDto>>>> GetTablesByFloor(
        [FromQuery] long floorId,
        CancellationToken ct)
    {
        var result = await _tableService.GetTablesByFloorAsync(floorId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a dining table by ID.
    /// </summary>
    [HttpGet("{tableId:long}")]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TableDto>>> GetTableById(
        long tableId,
        CancellationToken ct)
    {
        var result = await _tableService.GetTableByIdAsync(tableId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new dining table on a floor.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TableDto>>> CreateTable(
        [FromBody] CreateTableDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tableService.CreateTableAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates table configuration, number, capacity, and dimensions.
    /// </summary>
    [HttpPut("{tableId:long}")]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TableDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TableDto>>> UpdateTable(
        long tableId,
        [FromBody] UpdateTableDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tableService.UpdateTableAsync(tableId, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates a dining table.
    /// </summary>
    [HttpDelete("{tableId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTable(
        long tableId,
        CancellationToken ct)
    {
        var result = await _tableService.DeleteTableAsync(tableId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // =================== INTERACTIVE OPERATIONS ===================

    /// <summary>
    /// Updates table visual coordinates (drag-and-drop designer).
    /// </summary>
    [HttpPut("position")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateTablePosition(
        [FromBody] UpdateTablePositionDto dto,
        CancellationToken ct)
    {
        var result = await _tableService.UpdateTablePositionAsync(dto, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Updates table status (Available, Occupied, Billed, Cleaning, OutOfService).
    /// </summary>
    [HttpPut("{tableId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> ChangeTableStatus(
        long tableId,
        [FromQuery] PosTableStatus status,
        [FromQuery] long? activeOrderId,
        CancellationToken ct)
    {
        var result = await _tableService.ChangeTableStatusAsync(tableId, status, activeOrderId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Transfers an active order from one table to another.
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<bool>>> TransferTable(
        [FromBody] TransferTableDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _tableService.TransferTableAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
