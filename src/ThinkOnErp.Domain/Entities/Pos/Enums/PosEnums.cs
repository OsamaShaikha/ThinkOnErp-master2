namespace ThinkOnErp.Domain.Entities.Pos.Enums;

public enum PosShiftStatus
{
    Open = 1,
    Suspended = 2,
    BlindClosed = 3,
    AuditedAndClosed = 4
}

public enum PosCashMovementType
{
    FloatIn = 1,        // Opening cash addition
    CashDrop = 2,       // Mid-shift safe drop
    PayOut = 3,         // Petty cash expense
    TipPayout = 4       // Staff tips payout
}

public enum PosOrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3,
    Aggregator = 4,
    Kiosk = 5,
    QrTable = 6
}

public enum PosOrderStatus
{
    Draft = 1,
    Parked = 2,
    SentToKitchen = 3,
    Ready = 4,
    Completed = 5,
    Voided = 6,
    Refunded = 7
}

public enum PosPaymentMethod
{
    Cash = 1,
    Card = 2,
    Split = 3,
    CustomerAccount = 4,
    Cheque = 5,
    BankTransfer = 6,
    DigitalWallet = 7,
    LoyaltyPoints = 8,
    GiftCard = 9,
    AggregatorPaid = 10
}

public enum PosPromotionType
{
    Bogo = 1,                  // Buy X Get Y Free/Discounted
    MixAndMatch = 2,           // Choose N items from category for fixed price
    HappyHour = 3,             // Time/Day-of-week based discount
    TieredCartDiscount = 4,    // Spend > X get Y discount
    BundleCombo = 5            // Preset combo bundle discount
}

public enum PosDiscountScope
{
    Line = 1,
    Order = 2
}

public enum PosDiscountType
{
    Percentage = 1,
    FixedAmount = 2
}

public enum PosTableStatus
{
    Available = 1,
    Occupied = 2,
    Billed = 3,
    Cleaning = 4,
    Reserved = 5,
    OutOfService = 6
}

public enum PosKdsStatus
{
    Pending = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4
}

public enum ScaleBarcodeType
{
    Standard = 1,
    WeightEmbedded = 2,
    PriceEmbedded = 3
}

public enum PosReservationStatus
{
    Booked = 1,
    Confirmed = 2,
    Seated = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum PosInventoryConflictStatus
{
    PendingReview = 1,
    Resolved = 2,
    WrittenOff = 3
}

public enum PosAggregatorProvider
{
    Talabat = 1,
    Jahez = 2,
    Hungerstation = 3,
    Deliveroo = 4,
    UberEats = 5,
    CustomWebhook = 6
}

public enum PosSelectionType
{
    Single = 1,      // Radio button (choose exactly one, e.g. Size, Doneness)
    Multiple = 2     // Checkbox (choose multiple up to max, e.g. Toppings, Extras)
}
