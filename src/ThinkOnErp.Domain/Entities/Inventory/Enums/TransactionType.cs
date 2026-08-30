namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

/// <summary>
/// Defines the types of inventory transactions.
/// </summary>
public enum TransactionType
{
    GrnReceipt,
    OpeningBalance,
    SalesIssue,
    SalesReturn,
    PurchaseReturn,
    TransferOut,
    TransferIn,
    AdjustmentIn,
    AdjustmentOut,
    DamageWriteoff,
    WipIssue,
    AssemblyComplete,
    LandedCostAdj
}
