namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class GlVoucherTypeDto
{
    public long Id { get; set; }
    public int TypeCode { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SerialResetPolicy { get; set; } = string.Empty;
    public bool RequiresReview { get; set; }
    public bool AllowManualEntry { get; set; }
    public bool IsSystem { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
