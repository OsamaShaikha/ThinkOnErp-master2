namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlPdcRegister
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }

    /// <summary>
    /// RECEIVED (Inward from Customer) or ISSUED (Outward to Vendor)
    /// </summary>
    public string ChequeType { get; set; } = "RECEIVED";

    public string ChequeNumber { get; set; } = string.Empty;
    public DateTime ChequeDate { get; set; }
    public DateTime DueDate { get; set; }

    public decimal Amount { get; set; }
    public decimal LocalAmount { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string DrawerBankName { get; set; } = string.Empty;
    public string? DrawerBankAccountNo { get; set; }
    public string? BeneficiaryName { get; set; }

    public string? PartyType { get; set; } // CUSTOMER, VENDOR
    public string? PartyCode { get; set; }

    /// <summary>
    /// Status: RECEIVED, DEPOSITED, CLEARED, BOUNCED, CANCELLED
    /// </summary>
    public string Status { get; set; } = "RECEIVED";

    public string? IntermediateAccountCode { get; set; } // e.g. 111301 (Cheques Under Collection)
    public string? DepositBankAccountCode { get; set; }   // e.g. 111201 (Current Bank Account)
    public DateTime? DepositDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? BouncedDate { get; set; }
    public string? BounceReason { get; set; }

    public long? OriginatingVoucherId { get; set; }
    public long? ClearingVoucherId { get; set; }
    public long? BounceVoucherId { get; set; }

    public string? Notes { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public SysBranch? Branch { get; set; }
    public SysFiscalYear? FiscalYear { get; set; }
    public SysCurrency? Currency { get; set; }
    public GlVoucherHeader? OriginatingVoucher { get; set; }
    public GlVoucherHeader? ClearingVoucher { get; set; }
    public GlVoucherHeader? BounceVoucher { get; set; }
}
