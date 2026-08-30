namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class TaxTransaction
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public long TaxRateId { get; set; }

    /// <summary>AR_INVOICE, AP_BILL, GL_JOURNAL, PAYMENT, RECEIPT, CREDIT_NOTE, DEBIT_NOTE</summary>
    public string SourceModule { get; set; } = string.Empty;
    public long SourceDocumentId { get; set; }
    public string SourceDocumentNo { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public DateTime TaxDate { get; set; }

    /// <summary>CUSTOMER, VENDOR, OTHER</summary>
    public string? PartyType { get; set; }
    public string? PartyCode { get; set; }
    public string? PartyName { get; set; }
    public string? PartyTaxNumber { get; set; }

    public decimal BaseAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LocalBaseAmount { get; set; }
    public decimal LocalTaxAmount { get; set; }

    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>True for Output Tax (Sales), False for Input Tax (Purchases)</summary>
    public bool IsSalesTax { get; set; }

    /// <summary>True if Input tax can be reclaimed/recovered from the tax authority</summary>
    public bool IsRecoverable { get; set; } = true;

    public string? TaxGlAccountCode { get; set; }
    public long? GlVoucherId { get; set; }

    public string? Notes { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigations
    public TaxRate? TaxRate { get; set; }
    public GlVoucherHeader? GlVoucher { get; set; }
}
