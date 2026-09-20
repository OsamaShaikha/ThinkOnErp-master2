using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Serials;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Serials API: Manages individual product serial numbers, tracking, status updates, and lifecycle queries.
/// </summary>
[ApiController]
[Route("api/inventory/serials")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvSerialsController : ControllerBase
{
    private readonly IInvLotSerialRepository _lotSerialRepository;
    private readonly IInvItemRepository _itemRepository;

    public InvSerialsController(
        IInvLotSerialRepository lotSerialRepository,
        IInvItemRepository itemRepository)
    {
        _lotSerialRepository = lotSerialRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Retrieves a paginated and filterable list of serial numbers across all or specific items/warehouses.
    /// </summary>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="itemId">Optional filter by item identifier.</param>
    /// <param name="warehouseId">Optional filter by warehouse identifier.</param>
    /// <param name="status">Optional filter by serial status.</param>
    /// <param name="search">Optional search keyword (serial number, item code, or item name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of serial numbers.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<InvSerialListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSerials(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? itemId = null,
        [FromQuery] long? warehouseId = null,
        [FromQuery] SerialStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest(ApiResponse<PagedResultDto<InvSerialListDto>>.CreateFailure(
                "pageNumber and pageSize must be greater than zero.", statusCode: 400));
        }

        var (items, totalCount) = await _lotSerialRepository.GetSerialsPagedAsync(
            pageNumber, pageSize, itemId, warehouseId, status, search, cancellationToken);

        var dtos = items.Select(MapToListDto).ToList();
        var pagedResult = new PagedResultDto<InvSerialListDto>(dtos, totalCount, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResultDto<InvSerialListDto>>.CreateSuccess(
            pagedResult, "تم استرجاع قائمة الأرقام التسلسلية بنجاح"));
    }

    /// <summary>
    /// Retrieves full details of a specific serial number by its identifier.
    /// </summary>
    /// <param name="id">Serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Serial number details.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvSerialDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvSerialDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSerialById(long id, CancellationToken cancellationToken)
    {
        var serial = await _lotSerialRepository.GetSerialByIdAsync(id, cancellationToken);
        if (serial == null)
        {
            return NotFound(ApiResponse<InvSerialDetailDto>.CreateFailure("الرقم التسلسلي غير موجود", statusCode: 404));
        }

        var dto = new InvSerialDetailDto
        {
            Id = serial.Id,
            ItemId = serial.ItemId,
            ItemCode = serial.Item?.ItemCode ?? string.Empty,
            ItemNameLocal = serial.Item?.ItemNameLocal ?? string.Empty,
            ItemNameEn = serial.Item?.ItemNameEn,
            SerialNumber = serial.SerialNumber,
            LotId = serial.LotId,
            LotNumber = serial.Lot?.LotNumber,
            Status = serial.Status,
            CurrentWarehouseId = serial.CurrentWarehouseId,
            CurrentWarehouseName = serial.CurrentWarehouse?.WarehouseNameLocal,
            CurrentBinId = serial.CurrentBinId,
            CurrentBinCode = serial.CurrentBin?.BinCode.ToString(),
            ExpiryDate = serial.Lot?.ExpiryDate,
            ManufacturingDate = serial.Lot?.ManufacturingDate,
            StandardCost = serial.Item?.StandardCost ?? 0m
        };

        return Ok(ApiResponse<InvSerialDetailDto>.CreateSuccess(dto, "تم استرجاع بيانات الرقم التسلسلي بنجاح"));
    }

    /// <summary>
    /// Registers a new serial number for an item.
    /// </summary>
    /// <param name="request">Serial registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created serial number.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvSerialListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvSerialListDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSerial([FromBody] CreateSerialDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return BadRequest(ApiResponse<InvSerialListDto>.CreateFailure("رقم السيريال مطلوب", statusCode: 400));
        }

        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            return BadRequest(ApiResponse<InvSerialListDto>.CreateFailure("الصنف المحدد غير موجود", statusCode: 400));
        }

        var exists = await _lotSerialRepository.ExistsSerialNumberAsync(request.ItemId, request.SerialNumber.Trim(), cancellationToken);
        if (exists)
        {
            return BadRequest(ApiResponse<InvSerialListDto>.CreateFailure($"الرقم التسلسلي '{request.SerialNumber}' مسجل مسبقاً لهذا الصنف", statusCode: 400));
        }

        var serial = new InvSerialMaster
        {
            ItemId = request.ItemId,
            SerialNumber = request.SerialNumber.Trim(),
            LotId = request.LotId,
            Status = request.Status,
            CurrentWarehouseId = request.CurrentWarehouseId,
            CurrentBinId = request.CurrentBinId
        };

        await _lotSerialRepository.AddSerialAsync(serial, cancellationToken);
        await _lotSerialRepository.SaveChangesAsync(cancellationToken);

        var created = await _lotSerialRepository.GetSerialByIdAsync(serial.Id, cancellationToken);
        return Ok(ApiResponse<InvSerialListDto>.CreateSuccess(MapToListDto(created ?? serial), "تم تسجيل الرقم التسلسلي بنجاح"));
    }

    /// <summary>
    /// Updates the status and/or location of a serial number.
    /// </summary>
    /// <param name="id">Serial identifier.</param>
    /// <param name="request">Status update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated serial details.</returns>
    [HttpPut("{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<InvSerialListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvSerialListDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSerialStatus(long id, [FromBody] UpdateSerialStatusDto request, CancellationToken cancellationToken)
    {
        var serial = await _lotSerialRepository.GetSerialByIdAsync(id, cancellationToken);
        if (serial == null)
        {
            return NotFound(ApiResponse<InvSerialListDto>.CreateFailure("الرقم التسلسلي غير موجود", statusCode: 404));
        }

        serial.Status = request.Status;
        if (request.CurrentWarehouseId.HasValue)
            serial.CurrentWarehouseId = request.CurrentWarehouseId.Value;
        if (request.CurrentBinId.HasValue)
            serial.CurrentBinId = request.CurrentBinId.Value;

        await _lotSerialRepository.UpdateSerialAsync(serial, cancellationToken);
        await _lotSerialRepository.SaveChangesAsync(cancellationToken);

        var updated = await _lotSerialRepository.GetSerialByIdAsync(serial.Id, cancellationToken);
        return Ok(ApiResponse<InvSerialListDto>.CreateSuccess(MapToListDto(updated ?? serial), "تم تحديث حالة الرقم التسلسلي بنجاح"));
    }

    /// <summary>
    /// Deletes a serial number record (if not locked by posted stock movement).
    /// </summary>
    /// <param name="id">Serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success confirmation.</returns>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSerial(long id, CancellationToken cancellationToken)
    {
        var serial = await _lotSerialRepository.GetSerialByIdAsync(id, cancellationToken);
        if (serial == null)
        {
            return NotFound(ApiResponse<bool>.CreateFailure("الرقم التسلسلي غير موجود", statusCode: 404));
        }

        await _lotSerialRepository.DeleteSerialAsync(serial, cancellationToken);
        await _lotSerialRepository.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<bool>.CreateSuccess(true, "تم حذف الرقم التسلسلي بنجاح"));
    }

    private static InvSerialListDto MapToListDto(InvSerialMaster s)
    {
        return new InvSerialListDto
        {
            Id = s.Id,
            ItemId = s.ItemId,
            ItemCode = s.Item?.ItemCode ?? string.Empty,
            ItemNameLocal = s.Item?.ItemNameLocal ?? string.Empty,
            ItemNameEn = s.Item?.ItemNameEn,
            SerialNumber = s.SerialNumber,
            LotId = s.LotId,
            LotNumber = s.Lot?.LotNumber,
            Status = s.Status,
            CurrentWarehouseId = s.CurrentWarehouseId,
            CurrentWarehouseName = s.CurrentWarehouse?.WarehouseNameLocal,
            CurrentBinId = s.CurrentBinId,
            CurrentBinCode = s.CurrentBin?.BinCode.ToString()
        };
    }
}
