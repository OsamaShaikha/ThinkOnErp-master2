using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosTableService
{
    // Floors
    Task<ApiResponse<IReadOnlyList<FloorLayoutDto>>> GetFloorLayoutAsync(long branchId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<FloorDto>>> GetFloorsAsync(long branchId, CancellationToken ct = default);
    Task<ApiResponse<FloorDto>> GetFloorByIdAsync(long floorId, CancellationToken ct = default);
    Task<ApiResponse<FloorDto>> CreateFloorAsync(CreateFloorDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<FloorDto>> UpdateFloorAsync(long floorId, UpdateFloorDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteFloorAsync(long floorId, CancellationToken ct = default);

    // Tables
    Task<ApiResponse<IReadOnlyList<TableDto>>> GetTablesByFloorAsync(long floorId, CancellationToken ct = default);
    Task<ApiResponse<TableDto>> GetTableByIdAsync(long tableId, CancellationToken ct = default);
    Task<ApiResponse<TableDto>> CreateTableAsync(CreateTableDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TableDto>> UpdateTableAsync(long tableId, UpdateTableDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteTableAsync(long tableId, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateTablePositionAsync(UpdateTablePositionDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> ChangeTableStatusAsync(long tableId, PosTableStatus status, long? activeOrderId, CancellationToken ct = default);
    Task<ApiResponse<bool>> TransferTableAsync(TransferTableDto dto, string username, CancellationToken ct = default);
}

public class PosTableService : IPosTableService
{
    private readonly IPosTableRepository _tableRepository;
    private readonly IPosOrderRepository _orderRepository;

    public PosTableService(IPosTableRepository tableRepository, IPosOrderRepository orderRepository)
    {
        _tableRepository = tableRepository;
        _orderRepository = orderRepository;
    }

    // Floors
    public async Task<ApiResponse<IReadOnlyList<FloorLayoutDto>>> GetFloorLayoutAsync(long branchId, CancellationToken ct = default)
    {
        var floors = await _tableRepository.GetFloorsWithTablesAsync(branchId, ct);
        var result = floors.Select(f => new FloorLayoutDto
        {
            Id = f.Id,
            FloorCode = f.FloorCode,
            FloorName = f.FloorName,
            Tables = f.Tables.Select(t => new TableLayoutDto
            {
                Id = t.Id,
                TableNumber = t.TableNumber,
                TableName = t.TableName,
                Capacity = t.Capacity,
                Status = t.Status,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                Width = t.Width,
                Height = t.Height,
                Shape = t.Shape,
                ActiveOrderId = t.ActiveOrderId
            }).ToList()
        }).ToList();

        return ApiResponse<IReadOnlyList<FloorLayoutDto>>.CreateSuccess(result);
    }

    public async Task<ApiResponse<IReadOnlyList<FloorDto>>> GetFloorsAsync(long branchId, CancellationToken ct = default)
    {
        var floors = await _tableRepository.GetFloorsByBranchAsync(branchId, ct);
        var result = floors.Select(f => new FloorDto
        {
            Id = f.Id,
            BranchId = f.BranchId,
            FloorCode = f.FloorCode,
            FloorName = f.FloorName,
            SortOrder = f.SortOrder,
            IsActive = f.IsActive,
            TableCount = f.Tables?.Count ?? 0
        }).ToList();

        return ApiResponse<IReadOnlyList<FloorDto>>.CreateSuccess(result);
    }

    public async Task<ApiResponse<FloorDto>> GetFloorByIdAsync(long floorId, CancellationToken ct = default)
    {
        var floor = await _tableRepository.GetFloorByIdAsync(floorId, ct);
        if (floor == null)
            return ApiResponse<FloorDto>.CreateFailure("Floor not found", null, 404);

        return ApiResponse<FloorDto>.CreateSuccess(new FloorDto
        {
            Id = floor.Id,
            BranchId = floor.BranchId,
            FloorCode = floor.FloorCode,
            FloorName = floor.FloorName,
            SortOrder = floor.SortOrder,
            IsActive = floor.IsActive,
            TableCount = floor.Tables?.Count ?? 0
        });
    }

    public async Task<ApiResponse<FloorDto>> CreateFloorAsync(CreateFloorDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.FloorName) || string.IsNullOrWhiteSpace(dto.FloorCode))
            return ApiResponse<FloorDto>.CreateFailure("Floor name and floor code are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<FloorDto>.CreateFailure("Valid BranchId is required", null, 400);

        var floor = new PosFloor
        {
            BranchId = dto.BranchId,
            FloorCode = dto.FloorCode.Trim().ToUpper(),
            FloorName = dto.FloorName.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = true
        };

        await _tableRepository.AddFloorAsync(floor, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<FloorDto>.CreateSuccess(new FloorDto
        {
            Id = floor.Id,
            BranchId = floor.BranchId,
            FloorCode = floor.FloorCode,
            FloorName = floor.FloorName,
            SortOrder = floor.SortOrder,
            IsActive = floor.IsActive,
            TableCount = 0
        }, "Floor created successfully");
    }

    public async Task<ApiResponse<FloorDto>> UpdateFloorAsync(long floorId, UpdateFloorDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.FloorName))
            return ApiResponse<FloorDto>.CreateFailure("Floor name is required", null, 400);

        var floor = await _tableRepository.GetFloorByIdAsync(floorId, ct);
        if (floor == null)
            return ApiResponse<FloorDto>.CreateFailure("Floor not found", null, 404);

        floor.FloorName = dto.FloorName.Trim();
        floor.SortOrder = dto.SortOrder;
        floor.IsActive = dto.IsActive;

        await _tableRepository.UpdateFloorAsync(floor, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<FloorDto>.CreateSuccess(new FloorDto
        {
            Id = floor.Id,
            BranchId = floor.BranchId,
            FloorCode = floor.FloorCode,
            FloorName = floor.FloorName,
            SortOrder = floor.SortOrder,
            IsActive = floor.IsActive,
            TableCount = floor.Tables?.Count ?? 0
        }, "Floor updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteFloorAsync(long floorId, CancellationToken ct = default)
    {
        var floor = await _tableRepository.GetFloorByIdAsync(floorId, ct);
        if (floor == null)
            return ApiResponse<bool>.CreateFailure("Floor not found", null, 404);

        floor.IsActive = false;
        await _tableRepository.UpdateFloorAsync(floor, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Floor deactivated successfully");
    }

    // Tables
    public async Task<ApiResponse<IReadOnlyList<TableDto>>> GetTablesByFloorAsync(long floorId, CancellationToken ct = default)
    {
        var tables = await _tableRepository.GetTablesByFloorAsync(floorId, ct);
        var result = tables.Select(t => MapToTableDto(t)).ToList();
        return ApiResponse<IReadOnlyList<TableDto>>.CreateSuccess(result);
    }

    public async Task<ApiResponse<TableDto>> GetTableByIdAsync(long tableId, CancellationToken ct = default)
    {
        var table = await _tableRepository.GetTableByIdAsync(tableId, ct);
        if (table == null)
            return ApiResponse<TableDto>.CreateFailure("Table not found", null, 404);

        return ApiResponse<TableDto>.CreateSuccess(MapToTableDto(table));
    }

    public async Task<ApiResponse<TableDto>> CreateTableAsync(CreateTableDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.TableNumber) || dto.FloorId <= 0)
            return ApiResponse<TableDto>.CreateFailure("FloorId and TableNumber are required", null, 400);

        var table = new PosTable
        {
            FloorId = dto.FloorId,
            TableNumber = dto.TableNumber.Trim().ToUpper(),
            TableName = dto.TableName?.Trim(),
            Capacity = dto.Capacity > 0 ? dto.Capacity : 4,
            PositionX = dto.PositionX,
            PositionY = dto.PositionY,
            Width = dto.Width > 0 ? dto.Width : 80,
            Height = dto.Height > 0 ? dto.Height : 80,
            Shape = !string.IsNullOrWhiteSpace(dto.Shape) ? dto.Shape : "Square",
            Status = PosTableStatus.Available,
            IsActive = true
        };

        await _tableRepository.AddTableAsync(table, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<TableDto>.CreateSuccess(MapToTableDto(table), "Table created successfully");
    }

    public async Task<ApiResponse<TableDto>> UpdateTableAsync(long tableId, UpdateTableDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.TableNumber))
            return ApiResponse<TableDto>.CreateFailure("TableNumber is required", null, 400);

        var table = await _tableRepository.GetTableByIdAsync(tableId, ct);
        if (table == null)
            return ApiResponse<TableDto>.CreateFailure("Table not found", null, 404);

        table.TableNumber = dto.TableNumber.Trim().ToUpper();
        table.TableName = dto.TableName?.Trim();
        table.Capacity = dto.Capacity > 0 ? dto.Capacity : 4;
        table.PositionX = dto.PositionX;
        table.PositionY = dto.PositionY;
        table.Width = dto.Width > 0 ? dto.Width : 80;
        table.Height = dto.Height > 0 ? dto.Height : 80;
        table.Shape = !string.IsNullOrWhiteSpace(dto.Shape) ? dto.Shape : "Square";
        table.IsActive = dto.IsActive;

        await _tableRepository.UpdateTableAsync(table, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<TableDto>.CreateSuccess(MapToTableDto(table), "Table updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteTableAsync(long tableId, CancellationToken ct = default)
    {
        var table = await _tableRepository.GetTableByIdAsync(tableId, ct);
        if (table == null)
            return ApiResponse<bool>.CreateFailure("Table not found", null, 404);

        table.IsActive = false;
        await _tableRepository.UpdateTableAsync(table, ct);
        await _tableRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Table deactivated successfully");
    }

    public async Task<ApiResponse<bool>> UpdateTablePositionAsync(UpdateTablePositionDto dto, CancellationToken ct = default)
    {
        var table = await _tableRepository.GetTableByIdAsync(dto.TableId, ct);
        if (table == null)
            return ApiResponse<bool>.CreateFailure("Table not found", null, 404);

        table.PositionX = dto.PositionX;
        table.PositionY = dto.PositionY;
        table.Width = dto.Width;
        table.Height = dto.Height;

        await _tableRepository.SaveChangesAsync(ct);
        return ApiResponse<bool>.CreateSuccess(true, "Table position updated");
    }

    public async Task<ApiResponse<bool>> ChangeTableStatusAsync(long tableId, PosTableStatus status, long? activeOrderId, CancellationToken ct = default)
    {
        var table = await _tableRepository.GetTableByIdAsync(tableId, ct);
        if (table == null)
            return ApiResponse<bool>.CreateFailure("Table not found", null, 404);

        table.Status = status;
        table.ActiveOrderId = activeOrderId;
        table.StatusChangedAt = DateTime.UtcNow;

        await _tableRepository.SaveChangesAsync(ct);
        return ApiResponse<bool>.CreateSuccess(true, "Table status updated");
    }

    public async Task<ApiResponse<bool>> TransferTableAsync(TransferTableDto dto, string username, CancellationToken ct = default)
    {
        var fromTable = await _tableRepository.GetTableByIdAsync(dto.FromTableId, ct);
        if (fromTable == null)
            return ApiResponse<bool>.CreateFailure("Origin table not found", null, 404);

        var toTable = await _tableRepository.GetTableByIdAsync(dto.ToTableId, ct);
        if (toTable == null)
            return ApiResponse<bool>.CreateFailure("Target table not found", null, 404);

        if (toTable.Status != PosTableStatus.Available)
            return ApiResponse<bool>.CreateFailure("Target table is not available", null, 400);

        var activeOrderId = fromTable.ActiveOrderId;
        if (activeOrderId.HasValue)
        {
            var order = await _orderRepository.GetOrderByIdAsync(activeOrderId.Value, ct);
            if (order != null)
            {
                order.TableId = toTable.Id;
                order.Notes = (order.Notes ?? "") + $" [Transferred from table {fromTable.TableNumber} by {username}: {dto.Reason}]";
                await _orderRepository.UpdateOrderAsync(order, ct);
            }
        }

        // Switch table statuses
        toTable.Status = PosTableStatus.Occupied;
        toTable.ActiveOrderId = activeOrderId;
        toTable.StatusChangedAt = DateTime.UtcNow;

        fromTable.Status = PosTableStatus.Available;
        fromTable.ActiveOrderId = null;
        fromTable.StatusChangedAt = DateTime.UtcNow;

        await _tableRepository.SaveChangesAsync(ct);
        return ApiResponse<bool>.CreateSuccess(true, "Table transferred successfully");
    }

    private static TableDto MapToTableDto(PosTable t)
    {
        return new TableDto
        {
            Id = t.Id,
            FloorId = t.FloorId,
            TableNumber = t.TableNumber,
            TableName = t.TableName,
            Capacity = t.Capacity,
            Status = t.Status,
            PositionX = t.PositionX,
            PositionY = t.PositionY,
            Width = t.Width,
            Height = t.Height,
            Shape = t.Shape,
            ActiveOrderId = t.ActiveOrderId,
            IsActive = t.IsActive
        };
    }
}
