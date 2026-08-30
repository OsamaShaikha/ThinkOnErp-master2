namespace ThinkOnErp.Domain.Exceptions;

public sealed class InventoryException : DomainException
{
    public InventoryException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    public static class ErrorCodes
    {
        public const string ItemNotFound = "INV_001";
        public const string WarehouseNotFound = "INV_002";
        public const string InsufficientStock = "INV_003";
        public const string NegativeStockNotAllowed = "INV_004";
        public const string LotRequired = "INV_005";
        public const string SerialRequired = "INV_006";
        public const string FiscalPeriodClosed = "INV_007";
        public const string DuplicateItemCode = "INV_008";
        public const string DuplicateBarcode = "INV_009";
        public const string CostLayerExhausted = "INV_010";
        public const string InvalidTransferSameWarehouse = "INV_011";
        public const string AdjustmentNotApproved = "INV_012";
        public const string OpeningBalanceAlreadyPosted = "INV_013";
        public const string ReconciliationMismatch = "INV_014";
    }
}
