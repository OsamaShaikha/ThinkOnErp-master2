namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

/// <summary>
/// DTO for creating a new custom GL voucher type definition.
/// </summary>
public sealed class CreateGlVoucherTypeDto
{
    /// <summary>
    /// Numeric unique code for the voucher type (e.g. 101, 102, 401).
    /// </summary>
    public int TypeCode { get; set; }

    /// <summary>
    /// String key identifier for code references (e.g. JV, RV, PV, ADJ).
    /// </summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>
    /// Arabic display name.
    /// </summary>
    public string NameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English display name.
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Document prefix used in full serial numbering (e.g. JV, RV, PV).
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Voucher category: JOURNAL, RECEIPT, PAYMENT, SYSTEM.
    /// </summary>
    public string Category { get; set; } = "JOURNAL";

    /// <summary>
    /// Serial numbering reset policy: MONTHLY, YEARLY, CONTINUOUS.
    /// </summary>
    public string SerialResetPolicy { get; set; } = "MONTHLY";

    /// <summary>
    /// Whether vouchers of this type require manual review/audit approval before posting.
    /// </summary>
    public bool RequiresReview { get; set; } = true;

    /// <summary>
    /// Whether users are allowed to manually create vouchers of this type.
    /// </summary>
    public bool AllowManualEntry { get; set; } = true;

    /// <summary>
    /// Display sort order.
    /// </summary>
    public int DisplayOrder { get; set; } = 1;

    /// <summary>
    /// Active status flag.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional description or notes.
    /// </summary>
    public string? Description { get; set; }
}
