namespace ThinkOnErp.Application.DTOs.Inventory.StockMovements;

public class StockValuationDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal OnHandQty { get; set; }
    public decimal AvgCost { get; set; }
    public decimal TotalValue { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
}
