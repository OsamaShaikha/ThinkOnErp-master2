using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

public class OpeningBalanceBatchDto
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public long FiscalYearId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public List<OpeningBalanceLineDto> Lines { get; set; } = new();
}

public class OpeningBalanceLineDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? BinId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
