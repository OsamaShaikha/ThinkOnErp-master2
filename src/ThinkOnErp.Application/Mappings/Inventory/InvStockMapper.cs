using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvStockMapper
{
    public static StockMovementDto ToDto(InvStockLedgerEntry entity)
    {
        if (entity == null) return null!;

        return new StockMovementDto
        {
            Id = entity.Id,
            ItemId = entity.ItemId,
            ItemCode = entity.Item?.ItemCode ?? string.Empty,
            WarehouseId = entity.WarehouseId,
            WarehouseCode = entity.Warehouse?.WarehouseCode ?? 0,
            BinId = entity.BinId,
            TransactionType = (int)entity.TransactionType,
            Quantity = entity.Quantity,
            UomCode = entity.UomCode,
            UnitCost = entity.UnitCost,
            TotalCost = entity.Quantity * entity.UnitCost,
            LotNumber = entity.LotId?.ToString(),
            SerialNumber = entity.SerialId?.ToString(),
            LpnCode = entity.LpnCode,
            SourceModule = entity.SourceModule,
            SourceDocType = entity.SourceDocType,
            SourceDocId = entity.SourceDocId,
            Notes = entity.Notes,
            CreationDate = entity.CreationDate
        };
    }

    public static StockBalanceDto ToDto(InvStockBalance entity)
    {
        if (entity == null) return null!;

        return new StockBalanceDto
        {
            ItemId = entity.ItemId,
            ItemCode = entity.Item?.ItemCode ?? string.Empty,
            ItemName = entity.Item?.ItemNameLocal ?? string.Empty,
            WarehouseId = entity.WarehouseId,
            WarehouseCode = entity.Warehouse?.WarehouseCode ?? 0,
            OnHandQty = entity.OnHandQty,
            ReservedQty = entity.ReservedQty,
            AvailableQty = entity.OnHandQty - entity.ReservedQty,
            OnOrderQty = entity.OnOrderQty,
            AvgCost = entity.AvgCost,
            TotalValue = entity.OnHandQty * entity.AvgCost
        };
    }
}
