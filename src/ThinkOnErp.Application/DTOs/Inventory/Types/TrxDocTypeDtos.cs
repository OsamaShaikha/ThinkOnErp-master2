namespace ThinkOnErp.Application.DTOs.Inventory.Types;

public sealed class CreateTrxDocTypeDto
{
    public int TypeCode { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string TypeNameLocal { get; set; } = string.Empty;
    public string TypeNameEn { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = "INVENTORY";
    public string DocPrefix { get; set; } = string.Empty;
    public string ResetPolicy { get; set; } = "YEARLY";
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateTrxDocTypeDto
{
    public string TypeNameLocal { get; set; } = string.Empty;
    public string TypeNameEn { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = "INVENTORY";
    public string DocPrefix { get; set; } = string.Empty;
    public string ResetPolicy { get; set; } = "YEARLY";
    public bool IsActive { get; set; } = true;
}

public sealed class TrxDocTypeDto
{
    public int TypeCode { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string TypeNameLocal { get; set; } = string.Empty;
    public string TypeNameEn { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string DocPrefix { get; set; } = string.Empty;
    public string ResetPolicy { get; set; } = string.Empty;
    public bool IsSystemReserved { get; set; }
    public bool IsActive { get; set; }
    public List<TrxTransactionTypeSummaryDto> TransactionTypes { get; set; } = new();
}

public sealed class TrxTransactionTypeSummaryDto
{
    public int TrxCode { get; set; }
    public string TrxKey { get; set; } = string.Empty;
    public string TrxNameLocal { get; set; } = string.Empty;
    public string TrxNameEn { get; set; } = string.Empty;
    public bool AffectsStock { get; set; }
    public int StockDirection { get; set; }
    public bool AffectsGl { get; set; }
    public bool IsActive { get; set; }
}
