namespace ThinkOnErp.Application.DTOs.Accounting.Subledger;

public sealed class SubledgerTransactionDto
{
    public long Id { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string PartyNameLocal { get; set; } = string.Empty;
    public string PartyNameEn { get; set; } = string.Empty;
    public long JournalLineId { get; set; }
    public long VoucherId { get; set; }
    public string? VoucherNo { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal LocalAmount { get; set; }
    public decimal OpenAmount { get; set; }
    public decimal LocalOpenAmount { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Description { get; set; }
    public bool IsFullyPaid => OpenAmount == 0;
}

public sealed class ApplyCashDto
{
    public long PaymentTransactionId { get; set; }
    public List<InvoiceApplicationItemDto> Invoices { get; set; } = new();
}

public sealed class AutoApplyCashDto
{
    public long PaymentTransactionId { get; set; }
    public string? Notes { get; set; }
}

public sealed class InvoiceApplicationItemDto
{
    public long InvoiceTransactionId { get; set; }
    public decimal AmountToApply { get; set; }
    public string? Notes { get; set; }
}

public sealed class CashApplicationResultDto
{
    public long Id { get; set; }
    public long PaymentTransactionId { get; set; }
    public long InvoiceTransactionId { get; set; }
    public string? InvoiceReferenceNo { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal LocalAppliedAmount { get; set; }
    public decimal RealizedForexGainLoss { get; set; }
    public DateTime AppliedDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class CashApplicationDetailDto
{
    public long Id { get; set; }
    public long PaymentTransactionId { get; set; }
    public string? PaymentVoucherNo { get; set; }
    public DateTime PaymentDate { get; set; }
    public long InvoiceTransactionId { get; set; }
    public string? InvoiceReferenceNo { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal LocalAppliedAmount { get; set; }
    public DateTime AppliedDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}

public sealed class OpenInvoiceDto
{
    public long TransactionId { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal OpenAmount { get; set; }
    public decimal LocalOpenAmount { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Description { get; set; }
    public decimal ExchangeRate { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
}

public sealed class StatementRowDto
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class StatementOfAccountDto
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyNameLocal { get; set; } = string.Empty;
    public string PartyNameEn { get; set; } = string.Empty;
    public string PartyType { get; set; } = string.Empty; // CUSTOMER or VENDOR
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<StatementRowDto> Rows { get; set; } = new();
}

public sealed class AgingBucketDto
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyNameLocal { get; set; } = string.Empty;
    public string PartyNameEn { get; set; } = string.Empty;
    public decimal CurrentAmount { get; set; }    // Not yet due or 0-30 days
    public decimal Days31To60 { get; set; }      // 31-60 days overdue
    public decimal Days61To90 { get; set; }      // 61-90 days overdue
    public decimal Days91To120 { get; set; }     // 91-120 days overdue
    public decimal Over120Days { get; set; }     // > 120 days overdue
    public decimal TotalOutstanding { get; set; }
}

public sealed class AgingReportDto
{
    public string SubledgerType { get; set; } = string.Empty; // AR or AP
    public DateTime AsOfDate { get; set; }
    public decimal TotalCurrent { get; set; }
    public decimal Total31To60 { get; set; }
    public decimal Total61To90 { get; set; }
    public decimal Total91To120 { get; set; }
    public decimal TotalOver120 { get; set; }
    public decimal GrandTotal { get; set; }
    public List<AgingBucketDto> Buckets { get; set; } = new();
}

public sealed class SubledgerReconciliationDto
{
    public string SubledgerType { get; set; } = string.Empty; // AR or AP
    public string ControlAccountCode { get; set; } = string.Empty;
    public decimal GeneralLedgerBalance { get; set; }
    public decimal SubledgerTotalBalance { get; set; }
    public decimal Variance { get; set; }
    public bool IsBalanced => Math.Abs(Variance) < 0.001m;
}
