namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

/// <summary>
/// Defines the types of inventory transactions matching TRX_TRANSACTION_TYPE.
/// </summary>
public enum TransactionType
{
    LocalCashSales = 1001,
    SalesIssue = 1001,
    LocalCreditSales = 1002,
    ExportSales = 1003,
    PosSales = 1004,
    SalesReturn = 1501,
    SalesReturnRestock = 1501,
    SalesReturnScrap = 1502,
    GrnReceipt = 2001,
    LocalPurchase = 2001,
    ImportPurchase = 2003,
    PurchaseReturn = 2501,
    OpeningBalance = 3001,
    OpeningStock = 3001,
    AdjustmentIn = 3002,
    StockSurplus = 3002,
    FreeSamplesIn = 3003,
    AdjustmentOut = 3011,
    StockShortage = 3011,
    DamageWriteoff = 3012,
    ScrapWriteoff = 3012,
    InternalUse = 3014,
    TransferOut = 3501,
    TransferIn = 3501,
    InternalTransfer = 3501,
    SalesQuotation = 4001,
    PurchaseOrder = 4004
}
