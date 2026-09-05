using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class TrxDocumentLine
{
    public long BranchId { get; set; }
    public int DocYear { get; set; }
    public int DocType { get; set; }
    public long DocId { get; set; }
    public int LineNo { get; set; }
    public int TrxType { get; set; }
    public long ItemId { get; set; }
    public string? ItemDescription { get; set; }
    public int UomCode { get; set; }
    public decimal UomFactor { get; set; } = 1;
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal BaseQuantityIn { get; set; }
    public decimal BaseQuantityOut { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public long? FromBinId { get; set; }
    public long? ToBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? GlAccountCode { get; set; }
    public int? BaseLineNo { get; set; }

    public TrxDocumentHeader? DocumentHeader { get; set; }
    public InvItem? Item { get; set; }
    public TrxTransactionType? TrxTypeConfig { get; set; }
}
