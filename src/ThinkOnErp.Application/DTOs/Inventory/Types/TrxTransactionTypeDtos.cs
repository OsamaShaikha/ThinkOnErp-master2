namespace ThinkOnErp.Application.DTOs.Inventory.Types;

public sealed class CreateTrxTransactionTypeDto
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
}

public sealed class UpdateTrxTransactionTypeDto
{
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
}

public sealed class TrxTransactionTypeDto
{
    public int TrxCode { get; set; }
    public int DocTypeCode { get; set; }
    public string? DocTypeNameLocal { get; set; }
    public string? DocTypeNameEn { get; set; }
    public string TrxKey { get; set; } = string.Empty;
    public string TrxNameLocal { get; set; } = string.Empty;
    public string TrxNameEn { get; set; } = string.Empty;
    public bool AffectsStock { get; set; }
    public int StockDirection { get; set; }
    public bool RequiresWarehouse { get; set; }
    public bool AffectsGl { get; set; }
    public bool AffectsPartyBalance { get; set; }
    public string? PostingRuleCode { get; set; }
    public bool RequiresParty { get; set; }
    public bool RequiresPrice { get; set; }
    public bool RequiresCost { get; set; }
    public bool IsActive { get; set; }
}
