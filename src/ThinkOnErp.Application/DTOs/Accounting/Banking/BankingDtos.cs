namespace ThinkOnErp.Application.DTOs.Accounting.Banking;

public sealed class BankAccountDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranchName { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }

    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public string GlAccountCode { get; set; } = string.Empty;
    public string? GlAccountNameLocal { get; set; }

    public decimal OverdraftLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateBankAccountDto
{
    public long BranchId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranchName { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }

    public long CurrencyId { get; set; } = 1;
    public string GlAccountCode { get; set; } = "111201";
    public decimal OverdraftLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateBankAccountDto
{
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranchName { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }
    public string GlAccountCode { get; set; } = "111201";
    public decimal OverdraftLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CashRegisterDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string RegisterType { get; set; } = string.Empty;
    public string? CustodianName { get; set; }
    public string GlAccountCode { get; set; } = string.Empty;
    public string? GlAccountNameLocal { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }

    public decimal MinLimit { get; set; }
    public decimal MaxLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateCashRegisterDto
{
    public long BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string RegisterType { get; set; } = "MAIN"; // MAIN, BRANCH, CASHIER, PETTY_CASH
    public string? CustodianName { get; set; }
    public string GlAccountCode { get; set; } = "111101";
    public long CurrencyId { get; set; } = 1;

    public decimal MinLimit { get; set; }
    public decimal MaxLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateCashRegisterDto
{
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string RegisterType { get; set; } = "MAIN";
    public string? CustodianName { get; set; }
    public string GlAccountCode { get; set; } = "111101";
    public decimal MinLimit { get; set; }
    public decimal MaxLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BankStatementImportLineDto
{
    public DateTime TransactionDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? ReferenceNo { get; set; }
    public string? ChequeNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public sealed class CreateBankReconciliationDto
{
    public long BankAccountId { get; set; }
    public long FiscalYearId { get; set; }
    public long FiscalPeriodId { get; set; }
    public DateTime StatementDate { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public string? Notes { get; set; }
    public List<BankStatementImportLineDto> StatementLines { get; set; } = new();
}

public sealed class BankStatementLineDto
{
    public long Id { get; set; }
    public long ReconciliationId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? ReferenceNo { get; set; }
    public string? ChequeNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledDate { get; set; }
    public long? MatchedVoucherDetailId { get; set; }
}

public sealed class BankReconciliationDto
{
    public long Id { get; set; }
    public long BankAccountId { get; set; }
    public string? BankAccountNameLocal { get; set; }
    public string? BankName { get; set; }
    public long FiscalYearId { get; set; }
    public long FiscalPeriodId { get; set; }

    public DateTime StatementDate { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public decimal BookEndingBalance { get; set; }
    public decimal TotalReconciledAmount { get; set; }
    public decimal UnreconciledDifference { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string? Notes { get; set; }

    public List<BankStatementLineDto> Lines { get; set; } = new();
}

public sealed class AutoReconcileResultDto
{
    public long ReconciliationId { get; set; }
    public int TotalLines { get; set; }
    public int MatchedLinesCount { get; set; }
    public decimal MatchedAmount { get; set; }
    public int UnmatchedLinesCount { get; set; }
    public decimal UnreconciledDifference { get; set; }
}

public sealed class ManualMatchLineDto
{
    public long StatementLineId { get; set; }
    public long VoucherDetailId { get; set; }
}

public sealed class BankReconciliationStatementDto
{
    public string BankAccountNameLocal { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }

    public decimal BookBalance { get; set; }
    public decimal UnpresentedChequesAmount { get; set; } // شيكات لم تقدم للصرف بعد
    public decimal DepositsInTransitAmount { get; set; }   // إيداعات بالطريق لم تقيد بالبنك
    public decimal BankChargesNotRecorded { get; set; }   // مصاريف بنكية غير مسجلة بالدفاتر
    public decimal AdjustedBookBalance { get; set; }

    public decimal BankStatementBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsReconciled => Math.Abs(Difference) < 0.001m;
}
