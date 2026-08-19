using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Individual vendor payable transaction in the AP Subledger.
/// </summary>
public sealed class ApSubledgerTransaction
{
    public long Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public long JournalLineId { get; set; }
    public long VoucherId { get; set; }
    public string TransactionType { get; set; } = "BILL"; // BILL, PAYMENT, DEBIT_MEMO, CREDIT_MEMO, WRITE_OFF, ADJUSTMENT
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }

    public decimal Amount { get; set; } // Positive for credit (payable), negative for debit (payment/debit memo)
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
    public Vendor Vendor { get; set; } = null!;
    public GlVoucherHeader Voucher { get; set; } = null!;
    public GlVoucherDetail JournalLine { get; set; } = null!;
    public SysCurrency Currency { get; set; } = null!;

    public ICollection<ApCashApplication> PaymentApplications { get; set; } = new List<ApCashApplication>();
    public ICollection<ApCashApplication> InvoiceApplications { get; set; } = new List<ApCashApplication>();
}
