using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class TrxDocumentHeader
{
    public long BranchId { get; set; }
    public int DocYear { get; set; }
    public int DocType { get; set; }
    public long Id { get; set; }
    public int TrxType { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public DateTime DocDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public int PartyTypeCode { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }
    public long? FromWarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1;
    public int PaymentMethodCode { get; set; } = 1;
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
    public int DocStatusCode { get; set; } = 1;
    public bool IsPostedGl { get; set; }
    public bool IsPostedStock { get; set; }
    public long? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }

    public TrxDocType? DocTypeConfig { get; set; }
    public TrxTransactionType? TrxTypeConfig { get; set; }
    public InvWarehouse? FromWarehouse { get; set; }
    public InvWarehouse? ToWarehouse { get; set; }
    public List<TrxDocumentLine> Lines { get; set; } = new();
}
