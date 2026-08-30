using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

public class CreateOpeningBalanceBatchDto
{
    public string Description { get; set; } = string.Empty;
    public long FiscalYearId { get; set; }
    public List<CreateOpeningBalanceLineDto> Lines { get; set; } = new();
}

public class CreateOpeningBalanceLineDto
{
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? BinId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
