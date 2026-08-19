namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class CreateGlVoucherDetailDto
{
    public string AccountCode { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? CostCenterCode { get; set; }
    public string? CostCenterMgrCode { get; set; }
    public string? CostCenterMnrCode { get; set; }

    /// <summary>
    /// Party type: CUSTOMER, VENDOR, etc. Required when account is a control account (AR/AP).
    /// </summary>
    public string? PartyType { get; set; }

    /// <summary>
    /// Party code (customer/vendor code). Required when account is a control account (AR/AP).
    /// </summary>
    public string? PartyCode { get; set; }

    /// <summary>
    /// Line-level branch ID. Optional — defaults to voucher header BranchId if not specified.
    /// </summary>
    public long? BranchId { get; set; }
}
