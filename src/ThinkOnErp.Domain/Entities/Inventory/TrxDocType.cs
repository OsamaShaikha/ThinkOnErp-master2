using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class TrxDocType
{
    public int TypeCode { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string TypeNameLocal { get; set; } = string.Empty;
    public string TypeNameEn { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string DocPrefix { get; set; } = string.Empty;
    public string ResetPolicy { get; set; } = "YEARLY";
    public bool IsSystemReserved { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<TrxTransactionType> TransactionTypes { get; set; } = new();
}
