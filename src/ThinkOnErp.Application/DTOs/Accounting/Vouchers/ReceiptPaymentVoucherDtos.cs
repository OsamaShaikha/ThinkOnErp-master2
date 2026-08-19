namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class CreateReceiptVoucherDto
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "CASH"; // CASH, BANK, CHEQUE, POS
    public string CashOrBankAccountCode { get; set; } = "111101"; // Cash or Bank GL account
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal TotalAmount { get; set; }

    // If receipt is from a customer
    public string? CustomerCode { get; set; }

    // If direct income without customer
    public string? IncomeAccountCode { get; set; }
    public string? CostCenterCode { get; set; }

    // Cheque / Bank Transfer metadata
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? BankName { get; set; }
    public string? TransferReference { get; set; }

    // Optional invoices to settle immediately
    public List<ReceiptInvoiceSettlementItemDto> InvoicesToSettle { get; set; } = new();

    public bool AutoPost { get; set; } = true;
}

public sealed class UpdateReceiptVoucherDto
{
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public string CashOrBankAccountCode { get; set; } = "111101";
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal TotalAmount { get; set; }

    public string? CustomerCode { get; set; }
    public string? IncomeAccountCode { get; set; }
    public string? CostCenterCode { get; set; }

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? BankName { get; set; }
    public string? TransferReference { get; set; }
}

public sealed class ReceiptInvoiceSettlementItemDto
{
    public long InvoiceTransactionId { get; set; }
    public decimal AmountToApply { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReceiptVoucherDto
{
    public long VoucherId { get; set; }
    public long VoucherNo { get; set; }
    public long BranchId { get; set; }
    public string? BranchNameAr { get; set; }
    public string? BranchNameEn { get; set; }
    public DateTime VoucherDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string CashOrBankAccountCode { get; set; } = string.Empty;
    public string? CashOrBankAccountNameAr { get; set; }
    public string? CashOrBankAccountNameEn { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal LocalAmount { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Description { get; set; }

    public string? CustomerCode { get; set; }
    public string? CustomerNameAr { get; set; }
    public string? CustomerNameEn { get; set; }

    public string? IncomeAccountCode { get; set; }
    public string? IncomeAccountNameAr { get; set; }
    public string? IncomeAccountNameEn { get; set; }

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? BankName { get; set; }
    public string? TransferReference { get; set; }

    public int Status { get; set; } // 1: Draft, 2: Reviewed, 3: Posted, 4: Reversed
    public string StatusName { get; set; } = string.Empty;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }

    public List<ReceiptSettledInvoiceDto> SettledInvoices { get; set; } = new();
}

public sealed class ReceiptSettledInvoiceDto
{
    public long ApplicationId { get; set; }
    public long InvoiceTransactionId { get; set; }
    public string? InvoiceReferenceNo { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal LocalAppliedAmount { get; set; }
    public DateTime AppliedDate { get; set; }
}

public sealed class CreatePaymentVoucherDto
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "CASH"; // CASH, BANK, CHEQUE
    public string CashOrBankAccountCode { get; set; } = "111101"; // Cash or Bank GL account
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal TotalAmount { get; set; }

    // If payment is to a vendor
    public string? VendorCode { get; set; }

    // If direct expense without vendor
    public string? ExpenseAccountCode { get; set; }
    public string? CostCenterCode { get; set; }

    // Cheque / Bank Transfer metadata
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TransferReference { get; set; }

    // Optional bills to settle immediately
    public List<PaymentBillSettlementItemDto> BillsToSettle { get; set; } = new();

    public bool AutoPost { get; set; } = true;
}

public sealed class UpdatePaymentVoucherDto
{
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public string CashOrBankAccountCode { get; set; } = "111101";
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal TotalAmount { get; set; }

    public string? VendorCode { get; set; }
    public string? ExpenseAccountCode { get; set; }
    public string? CostCenterCode { get; set; }

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TransferReference { get; set; }
}

public sealed class PaymentBillSettlementItemDto
{
    public long BillTransactionId { get; set; }
    public decimal AmountToApply { get; set; }
    public string? Notes { get; set; }
}

public sealed class PaymentVoucherDto
{
    public long VoucherId { get; set; }
    public long VoucherNo { get; set; }
    public long BranchId { get; set; }
    public string? BranchNameAr { get; set; }
    public string? BranchNameEn { get; set; }
    public DateTime VoucherDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string CashOrBankAccountCode { get; set; } = string.Empty;
    public string? CashOrBankAccountNameAr { get; set; }
    public string? CashOrBankAccountNameEn { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal LocalAmount { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Description { get; set; }

    public string? VendorCode { get; set; }
    public string? VendorNameAr { get; set; }
    public string? VendorNameEn { get; set; }

    public string? ExpenseAccountCode { get; set; }
    public string? ExpenseAccountNameAr { get; set; }
    public string? ExpenseAccountNameEn { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenterNameAr { get; set; }

    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDueDate { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TransferReference { get; set; }

    public int Status { get; set; } // 1: Draft, 2: Reviewed, 3: Posted, 4: Reversed
    public string StatusName { get; set; } = string.Empty;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }

    public List<PaymentSettledBillDto> SettledBills { get; set; } = new();
}

public sealed class PaymentSettledBillDto
{
    public long ApplicationId { get; set; }
    public long BillTransactionId { get; set; }
    public string? BillReferenceNo { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal LocalAppliedAmount { get; set; }
    public DateTime AppliedDate { get; set; }
}
