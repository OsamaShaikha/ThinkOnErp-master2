namespace ThinkOnErp.Application.DTOs.Inventory.Reports;

public class InventoryValuationReportDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal OnHandQty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
}
