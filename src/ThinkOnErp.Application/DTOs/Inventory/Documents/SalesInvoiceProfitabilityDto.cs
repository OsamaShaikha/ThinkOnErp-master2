using System;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class SalesInvoiceProfitabilityDto
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

public sealed class ProfitabilitySummaryReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGrossProfit { get; set; }
    public decimal OverallProfitMarginPercent { get; set; }
    public int TotalInvoicesCount { get; set; }
    public int TotalLinesCount { get; set; }
    public IReadOnlyList<SalesInvoiceProfitabilityDto> Items { get; set; } = Array.Empty<SalesInvoiceProfitabilityDto>();
}
