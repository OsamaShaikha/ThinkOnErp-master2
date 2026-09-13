using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosBatchPrep
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime PrepDate { get; set; } = DateTime.UtcNow;

    // The prepared semi-finished or finished item
    public long OutputItemId { get; set; }
    public decimal PlannedOutputQty { get; set; }
    public decimal ActualOutputQty { get; set; }
    public int OutputUomId { get; set; }

    // Yield & Waste Calculation
    public decimal YieldFactor { get; set; } = 1.0m; // e.g. 0.85 if 15% waste
    public decimal TotalRawCost { get; set; }
    public decimal UnitCostProduced { get; set; }

    public string? PrepNotes { get; set; }
    public bool IsPostedToInventory { get; set; }
    public DateTime? PostedDate { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public InvItem? OutputItem { get; set; }
    public ICollection<PosBatchPrepLine> ConsumedLines { get; set; } = new List<PosBatchPrepLine>();
}

public class PosBatchPrepLine
{
    public long Id { get; set; }
    public long BatchPrepId { get; set; }
    public long RawItemId { get; set; }
    public decimal QuantityUsed { get; set; }
    public int UomId { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineCost { get; set; }
    public decimal ScrapWasteQty { get; set; }

    // Navigation
    public PosBatchPrep? BatchPrep { get; set; }
    public InvItem? RawItem { get; set; }
}
