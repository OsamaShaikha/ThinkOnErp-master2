using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.StockMovements;

public sealed class RecalculateCostRequestDto
{
    /// <summary>
    /// Optional: Specific Item ID to recalculate. If null, recalculates all active stock items.
    /// </summary>
    public long? ItemId { get; set; }

    /// <summary>
    /// Optional: Specific Warehouse ID. If null, recalculates across all warehouses.
    /// </summary>
    public long? WarehouseId { get; set; }

    /// <summary>
    /// Optional: Start date for historical recalculation replay.
    /// </summary>
    public DateTime? FromDate { get; set; }
}

public sealed class RecalculateCostResultDto
{
    public int TotalItemsProcessed { get; set; }
    public int TotalMovementsRecalculated { get; set; }
    public int TotalInvoiceLinesUpdated { get; set; }
    public List<ItemCostRecalculationSummaryDto> Details { get; set; } = new();
}

public sealed class ItemCostRecalculationSummaryDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal OldAvgCost { get; set; }
    public decimal NewAvgCost { get; set; }
    public int MovementsCount { get; set; }
    public int InvoiceLinesCount { get; set; }
}
