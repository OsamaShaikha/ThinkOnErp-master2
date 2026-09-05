using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class TrxTransactionType
{
    public int TrxCode { get; set; }
    public int DocTypeCode { get; set; }
    public string TrxKey { get; set; } = string.Empty;
    public string TrxNameLocal { get; set; } = string.Empty;
    public string TrxNameEn { get; set; } = string.Empty;
    public bool AffectsStock { get; set; } = true;
    public int StockDirection { get; set; } = 0;
    public bool RequiresWarehouse { get; set; } = true;
    public bool AffectsGl { get; set; } = true;
    public bool AffectsPartyBalance { get; set; } = false;
    public string? PostingRuleCode { get; set; }
    public bool RequiresParty { get; set; } = false;
    public bool RequiresPrice { get; set; } = false;
    public bool RequiresCost { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public TrxDocType? DocType { get; set; }
}
