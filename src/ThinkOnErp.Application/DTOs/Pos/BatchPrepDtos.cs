using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreateBatchPrepDto
{
    public long BranchId { get; set; }
    public long OutputItemId { get; set; }
    public decimal PlannedOutputQty { get; set; }
    public decimal ActualOutputQty { get; set; }
    public int OutputUomId { get; set; }
    public decimal YieldFactor { get; set; } = 1.0m;
    public string? PrepNotes { get; set; }

    public List<CreateBatchPrepLineDto> ConsumedLines { get; set; } = new();
}

public class CreateBatchPrepLineDto
{
    public long RawItemId { get; set; }
    public decimal QuantityUsed { get; set; }
    public int UomId { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ScrapWasteQty { get; set; }
}

public class BatchPrepSummaryDto
{
    public long Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime PrepDate { get; set; }
    public long OutputItemId { get; set; }
    public decimal PlannedOutputQty { get; set; }
    public decimal ActualOutputQty { get; set; }
    public decimal YieldFactor { get; set; }
    public decimal TotalRawCost { get; set; }
    public decimal UnitCostProduced { get; set; }
    public bool IsPostedToInventory { get; set; }
}
