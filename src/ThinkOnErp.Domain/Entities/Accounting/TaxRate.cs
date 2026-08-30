namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class TaxRate
{
    public long Id { get; set; }
    public string TaxRateCode { get; set; } = string.Empty; // VAT_15, VAT_5, VAT_0, VAT_EXEMPT, VAT_OUT_OF_SCOPE, WHT_5
    public long TaxCategoryId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Rate percentage, e.g. 15.00, 5.00, 0.00</summary>
    public decimal RatePercent { get; set; }

    /// <summary>PERCENTAGE or FIXED_AMOUNT</summary>
    public string RateType { get; set; } = "PERCENTAGE";

    /// <summary>GL Account for Output Tax (Sales / Invoices Payable to Tax Authority)</summary>
    public string? SalesTaxGlAccountCode { get; set; }

    /// <summary>GL Account for Input Tax (Purchases / Bills Receivable from Tax Authority)</summary>
    public string? PurchaseTaxGlAccountCode { get; set; }

    public bool IsExempt { get; set; }
    public bool IsZeroRated { get; set; }

    /// <summary>ZATCA or legal exemption reason code, e.g. VATEX-SA-32, VATEX-SA-29-7</summary>
    public string? ExemptionReasonCode { get; set; }
    public string? ExemptionReasonAr { get; set; }
    public string? ExemptionReasonEn { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigations
    public TaxCategory? Category { get; set; }
    public GlAccount? SalesTaxGlAccount { get; set; }
    public GlAccount? PurchaseTaxGlAccount { get; set; }
}
