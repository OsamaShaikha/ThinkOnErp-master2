namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

/// <summary>
/// Defines the statuses for a transfer order.
/// </summary>
public enum TransferStatus
{
    Draft,
    Approved,
    InTransit,
    Received,
    Cancelled
}
