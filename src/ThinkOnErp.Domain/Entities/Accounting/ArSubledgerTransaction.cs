using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Individual customer receivable transaction in the AR Subledger.
/// </summary>
public sealed class ArSubledgerTransaction
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public long JournalLineId { get; set; }
    public long VoucherId { get; set; }
    public string TransactionType { get; set; } = "INVOICE"; // INVOICE, RECEIPT, CREDIT_MEMO, DEBIT_MEMO, WRITE_OFF, ADJUSTMENT
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }

    public decimal Amount { get; set; } // Positive for debit (receivable), negative for credit (receipt/credit memo)
    public long CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal LocalAmount { get; set; }

    public decimal OpenAmount { get; set; } // Remaining unpaid / unapplied amount
    public decimal LocalOpenAmount { get; set; }

    public string? ReferenceNo { get; set; }
    public string? Description { get; set; }

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public GlVoucherHeader Voucher { get; set; } = null!;
    public GlVoucherDetail JournalLine { get; set; } = null!;
    public SysCurrency Currency { get; set; } = null!;

    public ICollection<ArCashApplication> PaymentApplications { get; set; } = new List<ArCashApplication>();
    public ICollection<ArCashApplication> InvoiceApplications { get; set; } = new List<ArCashApplication>();
}
