namespace ThinkOnErp.Application.DTOs.Accounting.AccountStatement;

/// <summary>
/// Represents a single transaction movement row in an Account Statement.
/// </summary>
public sealed class AccountStatementItemDto
{
    public long VoucherId { get; set; }
    public long VoucherNo { get; set; }
    public DateTime VoucherDate { get; set; }

    public int VoucherTypeCode { get; set; }
    public string VoucherTypeNameAr { get; set; } = string.Empty;
    public string VoucherTypeNameEn { get; set; } = string.Empty;
    public string VoucherPrefix { get; set; } = string.Empty;

    public int LineSer { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }

    /// <summary>
    /// Cumulative running balance up to and including this transaction.
    /// </summary>
    public decimal RunningBalance { get; set; }

    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? CostCenterCode { get; set; }
    public string? CostCenterNameAr { get; set; }
    public string? CostCenterNameEn { get; set; }

    public string? Description { get; set; }
    public string? SourceSystemCode { get; set; }
    public long? SourceRefId { get; set; }
    public int VoucherStatus { get; set; }
}
