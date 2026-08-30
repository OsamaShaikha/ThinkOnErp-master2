using System;

namespace ThinkOnErp.Application.DTOs.Inventory.Reports;

public class AtpResultDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal OnOrder { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal Atp { get; set; }
    public DateTime? EarliestAvailableDate { get; set; }
}
