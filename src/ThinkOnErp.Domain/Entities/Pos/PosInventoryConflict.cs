using System;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosInventoryConflict
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long OrderId { get; set; }
    public long ItemId { get; set; }
    public decimal SoldQuantity { get; set; }
    public decimal AvailableStockAtSync { get; set; }
    public decimal DeficitQuantity { get; set; }
    public PosInventoryConflictStatus Status { get; set; } = PosInventoryConflictStatus.PendingReview;

    public string? ResolutionNotes { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public PosOrderHeader? Order { get; set; }
    public InvItem? Item { get; set; }
}
