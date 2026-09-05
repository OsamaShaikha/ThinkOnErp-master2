using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

public sealed class OpeningBatchDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime BatchDate { get; set; }
    public long FiscalYearId { get; set; }
    public string? Description { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValuationAmount { get; set; }
    public int StatusCode { get; set; }
    public long? JournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedBy { get; set; }

    public List<OpeningLineDto> Lines { get; set; } = new();
}

public sealed class OpeningLineDto
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public long? BinId { get; set; }
    public int UomCode { get; set; }
    public decimal UomFactor { get; set; } = 1;
    public decimal Quantity { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}
