namespace ThinkOnErp.Application.DTOs.Accounting.Tax;

// ─────────────────────────────────────────────────────────────────────────────
// Tax Master Data DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TaxCategoryDto
{
    public long Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int RatesCount { get; set; }
}

public sealed class CreateTaxCategoryDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 1;
}

public sealed class UpdateTaxCategoryDto
{
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public sealed class TaxRateDto
{
    public long Id { get; set; }
    public string TaxRateCode { get; set; } = string.Empty;
    public long TaxCategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public string RateType { get; set; } = "PERCENTAGE";

    public string? SalesTaxGlAccountCode { get; set; }
    public string? SalesTaxGlAccountNameLocal { get; set; }

    public string? PurchaseTaxGlAccountCode { get; set; }
    public string? PurchaseTaxGlAccountNameLocal { get; set; }

    public bool IsExempt { get; set; }
    public bool IsZeroRated { get; set; }
    public string? ExemptionReasonCode { get; set; }
    public string? ExemptionReasonLocal { get; set; }
    public string? ExemptionReasonEn { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

public sealed class CreateTaxRateDto
{
    public string TaxRateCode { get; set; } = string.Empty;
    public long TaxCategoryId { get; set; }
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public string RateType { get; set; } = "PERCENTAGE";

    public string? SalesTaxGlAccountCode { get; set; }
    public string? PurchaseTaxGlAccountCode { get; set; }

    public bool IsExempt { get; set; }
    public bool IsZeroRated { get; set; }
    public string? ExemptionReasonCode { get; set; }
    public string? ExemptionReasonLocal { get; set; }
    public string? ExemptionReasonEn { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public string? Description { get; set; }
}

public sealed class UpdateTaxRateDto
{
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public string RateType { get; set; } = "PERCENTAGE";

    public string? SalesTaxGlAccountCode { get; set; }
    public string? PurchaseTaxGlAccountCode { get; set; }

    public bool IsExempt { get; set; }
    public bool IsZeroRated { get; set; }
    public string? ExemptionReasonCode { get; set; }
    public string? ExemptionReasonLocal { get; set; }
    public string? ExemptionReasonEn { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public sealed class TaxGroupDto
{
    public long Id { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<TaxGroupItemDto> Items { get; set; } = new();
}

public sealed class TaxGroupItemDto
{
    public long Id { get; set; }
    public long TaxRateId { get; set; }
    public string TaxRateCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public int ApplicationOrder { get; set; }
    public bool IsCompound { get; set; }
}

public sealed class CreateTaxGroupDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CreateTaxGroupItemDto> Items { get; set; } = new();
}

public sealed class CreateTaxGroupItemDto
{
    public long TaxRateId { get; set; }
    public int ApplicationOrder { get; set; } = 1;
    public bool IsCompound { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tax Calculation Engine DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TaxCalculationRequestDto
{
    public bool IsSales { get; set; } = true; // true = Sales (Output VAT), false = Purchase (Input VAT)
    public bool IsInclusive { get; set; } // true = price entered includes tax
    public decimal ExchangeRate { get; set; } = 1.0m;
    public List<TaxCalculationLineRequestDto> Lines { get; set; } = new();
}

public sealed class TaxCalculationLineRequestDto
{
    public int LineNumber { get; set; } = 1;
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; } = 1.0m;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; } // Discount on this line
    public string? TaxRateCode { get; set; } // e.g. VAT_15, VAT_0, VAT_EXEMPT
    public long? TaxRateId { get; set; }
}

public sealed class TaxCalculationResultDto
{
    public decimal SubtotalAmount { get; set; } // Net base before tax
    public decimal TotalDiscountAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal GrandTotalAmount { get; set; }

    public decimal LocalSubtotalAmount { get; set; }
    public decimal LocalTotalTaxAmount { get; set; }
    public decimal LocalGrandTotalAmount { get; set; }

    public List<TaxCalculationLineResultDto> Lines { get; set; } = new();
    public List<TaxSummaryItemDto> TaxSummary { get; set; } = new();
    public List<SuggestedGlJournalLineDto> SuggestedGlLines { get; set; } = new();
}

public sealed class TaxCalculationLineResultDto
{
    public int LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal GrossAmount { get; set; } // Quantity * UnitPrice
    public decimal DiscountAmount { get; set; }
    public decimal NetBaseAmount { get; set; } // Gross - Discount (or extracted if inclusive)
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotalAmount { get; set; } // NetBase + Tax

    public decimal LocalNetBaseAmount { get; set; }
    public decimal LocalTaxAmount { get; set; }
    public decimal LocalLineTotalAmount { get; set; }

    public string TaxRateCode { get; set; } = string.Empty;
    public string TaxRateNameLocal { get; set; } = string.Empty;
    public string? GlAccountCode { get; set; }
}

public sealed class TaxSummaryItemDto
{
    public string TaxRateCode { get; set; } = string.Empty;
    public string TaxRateNameLocal { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public decimal TotalBaseAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal LocalTotalBaseAmount { get; set; }
    public decimal LocalTotalTaxAmount { get; set; }
    public string? GlAccountCode { get; set; }
}

public sealed class SuggestedGlJournalLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }
    public string Description { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Multi-Country VAT / Tax Declaration Template DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TaxDeclarationTemplateDto
{
    public string TemplateCode { get; set; } = string.Empty; // GCC_ZATCA_16, JO_SALES_TAX, EG_VAT_FORM_10, UK_EU_VAT_9, GLOBAL_GENERIC
    public string CountryCode { get; set; } = string.Empty;   // SA, JO, EG, GB, GLOBAL
    public string CountryNameLocal { get; set; } = string.Empty;
    public string CountryNameEn { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string TaxAuthorityNameLocal { get; set; } = string.Empty;
    public string TaxAuthorityNameEn { get; set; } = string.Empty;
    public string DefaultCurrencyCode { get; set; } = string.Empty;
    public string FilingFrequency { get; set; } = "MONTHLY_OR_QUARTERLY";
    public List<TaxDeclarationTemplateSectionDto> Sections { get; set; } = new();
}

public sealed class TaxDeclarationTemplateSectionDto
{
    public string SectionKey { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public List<TaxDeclarationTemplateBoxDto> Boxes { get; set; } = new();
}

public sealed class TaxDeclarationTemplateBoxDto
{
    public int BoxNumber { get; set; }
    public string BoxKey { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public bool IsCalculated { get; set; }
    public string? CalculationFormula { get; set; }
}

public sealed class VatDeclarationFilterDto
{
    public long? BranchId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? TemplateCode { get; set; } // Optional: GCC_ZATCA_16, JO_SALES_TAX, EG_VAT_FORM_10, UK_EU_VAT_9, GLOBAL_GENERIC
}

public sealed class VatDeclarationDto
{
    public string TemplateCode { get; set; } = "GCC_ZATCA_16";
    public string CountryCode { get; set; } = "SA";
    public string TemplateTitleAr { get; set; } = string.Empty;
    public string TemplateTitleEn { get; set; } = string.Empty;
    public string TaxAuthorityNameLocal { get; set; } = string.Empty;
    public string TaxAuthorityNameEn { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "SAR";

    public long? BranchId { get; set; }
    public string BranchNameLocal { get; set; } = "كافة الفروع";
    public string BranchNameEn { get; set; } = "All Branches";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Structured multi-section output
    public List<TaxDeclarationSectionResultDto> Sections { get; set; } = new();

    // Backward-compatible properties for existing consumers & UI
    public List<VatBoxItemDto> SalesBoxes { get; set; } = new();
    public decimal TotalSalesBaseAmount { get; set; }
    public decimal TotalOutputVatAmount { get; set; }

    public List<VatBoxItemDto> PurchaseBoxes { get; set; } = new();
    public decimal TotalPurchasesBaseAmount { get; set; }
    public decimal TotalInputVatAmount { get; set; }

    public decimal NetVatDueAmount { get; set; } // Output VAT - Input VAT (Positive = Pay, Negative = Refund)
    public string NetVatStatus => NetVatDueAmount >= 0 ? "مستحق السداد للهيئة" : "رصيد مسترد من الهيئة";
}

public sealed class TaxDeclarationSectionResultDto
{
    public string SectionKey { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public decimal TotalBaseAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public List<VatBoxItemDto> Boxes { get; set; } = new();
}

public sealed class VatBoxItemDto
{
    public int BoxNumber { get; set; }
    public string BoxKey { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal VatAmount { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// ZATCA QR Code DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ZatcaQrRequestDto
{
    public string SellerName { get; set; } = string.Empty;
    public string TaxRegistrationNumber { get; set; } = string.Empty;
    public DateTime InvoiceTimestamp { get; set; }
    public decimal InvoiceTotalWithVat { get; set; }
    public decimal VatTotal { get; set; }
}

public sealed class ZatcaQrResultDto
{
    public string QrBase64 { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string TaxRegistrationNumber { get; set; } = string.Empty;
    public DateTime InvoiceTimestamp { get; set; }
    public decimal InvoiceTotalWithVat { get; set; }
    public decimal VatTotal { get; set; }
}
