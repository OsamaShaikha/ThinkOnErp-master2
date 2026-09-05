namespace ThinkOnErp.Domain.Entities.Views;

/// <summary>
/// Keyless view entity mapped to database view VW_SALES_INVOICE_PROFITABILITY.
/// Optimized for invoice and line-item level profitability analytics (Revenue, Cost, Gross Profit, Profit Margin %).
/// </summary>
public sealed class SalesInvoiceProfitabilityView
{
    public long BranchId { get; set; }
    public int DocYear { get; set; }
    public int DocType { get; set; }
    public long DocId { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public DateTime DocDate { get; set; }
    public int DocStatusCode { get; set; }
    public bool IsPostedGl { get; set; }
    public bool IsPostedStock { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public long? FromWarehouseId { get; set; }
    public decimal HeaderTotalGross { get; set; }
    public decimal HeaderTotalNet { get; set; }
    public decimal HeaderTotalCost { get; set; }
    public decimal HeaderTotalProfit { get; set; }
    public decimal HeaderProfitMargin { get; set; }
    public int LineNo { get; set; }
    public long? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public decimal LineCost { get; set; }
    public decimal LineProfit { get; set; }
    public decimal LineProfitMargin { get; set; }
}
