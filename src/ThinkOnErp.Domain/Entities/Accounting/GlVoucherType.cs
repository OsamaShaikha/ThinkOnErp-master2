namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlVoucherType
{
    public long Id { get; set; }
    public int TypeCode { get; set; }
    public string TypeKey { get; set; } = string.Empty; // JV, RV, PV, SALES, etc.

    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty; // JV, RV, PV, INV...

    public string Category { get; set; } = "JOURNAL"; // JOURNAL, RECEIPT, PAYMENT, SYSTEM
    public string SerialResetPolicy { get; set; } = "MONTHLY"; // MONTHLY, YEARLY, CONTINUOUS

    public bool RequiresReview { get; set; } = true;
    public bool AllowManualEntry { get; set; } = true;
    public bool IsSystem { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
