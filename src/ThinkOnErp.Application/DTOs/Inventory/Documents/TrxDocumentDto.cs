using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class TrxDocumentDto
{
    public long BranchId { get; set; }
    public int DocYear { get; set; }
    public int DocType { get; set; }
    public long Id { get; set; }
    public int TrxType { get; set; }
    public string DocTypeName { get; set; } = string.Empty;
    public string TrxTypeName { get; set; } = string.Empty;
    public string DocNo { get; set; } = string.Empty;
    public DateTime DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int PartyTypeCode { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }
    public long? FromWarehouseId { get; set; }
    public string? FromWarehouseName { get; set; }
    public long? ToWarehouseId { get; set; }
    public string? ToWarehouseName { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public int PaymentMethodCode { get; set; }
    public decimal TotalGross { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalNetBeforeTax { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalNet { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public long? BaseBranchId { get; set; }
    public int? BaseDocYear { get; set; }
    public int? BaseDocType { get; set; }
    public long? BaseDocId { get; set; }
    public int DocStatusCode { get; set; }
    public bool IsPostedGl { get; set; }
    public bool IsPostedStock { get; set; }
    public long? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }

    public List<TrxDocumentLineDto> Lines { get; set; } = new();
}

public sealed class TrxDocumentLineDto
{
    public int LineNo { get; set; }
    public int TrxType { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal UomFactor { get; set; }
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
}
