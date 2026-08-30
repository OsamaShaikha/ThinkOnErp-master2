using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Warehouses;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Warehouses, Zones and Bins API: Manages physical warehouse sites, functional storage zones, and granular bin locations.
/// </summary>
[ApiController]
[Route("api/inventory/warehouses")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvWarehousesController : ControllerBase
{
    private readonly IInvWarehouseService _warehouseService;

    public InvWarehousesController(IInvWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    /// <summary>
    /// Creates a new warehouse site for a branch.
    /// </summary>
    /// <param name="request">Warehouse creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created warehouse details.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvWarehouseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateWarehouse([FromBody] CreateInvWarehouseDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.CreateAsync(request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehouseCreated;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves all active warehouses configured for a specified branch.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of warehouses.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<InvWarehouseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouses([FromQuery] long branchId, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.GetAllByBranchAsync(branchId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehousesRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves details of a specific warehouse including its configured zones and bins.
    /// </summary>
    /// <param name="id">Warehouse identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Warehouse details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<InvWarehouseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvWarehouseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWarehouseById(long id, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.GetByIdAsync(id, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehouseDetailsRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Updates warehouse name, address, or bin tracking settings.
    /// </summary>
    /// <param name="id">Warehouse identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated warehouse profile.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<InvWarehouseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWarehouse(long id, [FromBody] UpdateInvWarehouseDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehouseUpdated;

        return Ok(response);
    }

    /// <summary>
    /// Deactivates or deletes a warehouse.
    /// </summary>
    /// <param name="id">Warehouse identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWarehouse(long id, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.DeleteWarehouseAsync(id, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehouseDeleted;

        return Ok(response);
    }

    /// <summary>
    /// Adds a functional zone (Storage, Receiving, Staging, Quarantine, Damaged, Returns) to a warehouse.
    /// </summary>
    /// <param name="id">Warehouse identifier.</param>
    /// <param name="request">Zone creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created zone details.</returns>
    [HttpPost("{id}/zones")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddZone(long id, [FromBody] CreateInvZoneDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.AddZoneAsync(id, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ZoneCreated;

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing zone.
    /// </summary>
    /// <param name="zoneId">Zone identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update confirmation.</returns>
    [HttpPut("zones/{zoneId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateZone(long zoneId, [FromBody] UpdateInvZoneDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.UpdateZoneAsync(zoneId, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ZoneUpdated;

        return Ok(response);
    }

    /// <summary>
    /// Deletes a zone from a warehouse.
    /// </summary>
    /// <param name="zoneId">Zone identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("zones/{zoneId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteZone(long zoneId, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.DeleteZoneAsync(zoneId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ZoneDeleted;

        return Ok(response);
    }

    /// <summary>
    /// Registers a granular bin rack/shelf location with max weight and volume capacities.
    /// </summary>
    /// <param name="zoneId">Zone identifier.</param>
    /// <param name="request">Bin creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created bin details.</returns>
    [HttpPost("zones/{zoneId:long}/bins")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddBin(long zoneId, [FromBody] CreateInvBinDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.AddBinAsync(zoneId, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.BinCreated;

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing storage bin.
    /// </summary>
    /// <param name="binId">Bin identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update confirmation.</returns>
    [HttpPut("bins/{binId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBin(long binId, [FromBody] UpdateInvBinDto request, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.UpdateBinAsync(binId, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.BinUpdated;

        return Ok(response);
    }

    /// <summary>
    /// Deletes a storage bin.
    /// </summary>
    /// <param name="binId">Bin identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("bins/{binId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBin(long binId, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.DeleteBinAsync(binId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.BinDeleted;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a consolidated real-time stock balance summary for all items in a warehouse.
    /// </summary>
    /// <param name="id">Warehouse identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consolidated warehouse stock summary.</returns>
    [HttpGet("{id}/stock")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouseStock(long id, CancellationToken cancellationToken)
    {
        var response = await _warehouseService.GetStockSummaryAsync(id, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.WarehouseStockRetrieved;

        return Ok(response);
    }
}
