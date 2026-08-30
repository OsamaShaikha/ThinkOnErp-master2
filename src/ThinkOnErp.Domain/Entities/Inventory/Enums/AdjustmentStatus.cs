namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

/// <summary>
/// Defines the statuses for an inventory adjustment.
/// </summary>
public enum AdjustmentStatus
{
    Draft,
    PendingApproval,
    Approved,
    Posted,
    Rejected
}
