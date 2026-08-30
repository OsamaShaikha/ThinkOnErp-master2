using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvOpeningBatch
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime BatchDate { get; set; } = DateTime.UtcNow;
    public long FiscalYearId { get; set; }
    public string? Description { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValuationAmount { get; set; }
    public int StatusCode { get; set; } = 1;
    public long? JournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedBy { get; set; }
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<InvOpeningLine> Lines { get; set; } = new();
}
