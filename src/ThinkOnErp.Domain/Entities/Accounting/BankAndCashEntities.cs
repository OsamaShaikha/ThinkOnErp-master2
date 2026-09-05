namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class BankAccount
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranchName { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }

    public long CurrencyId { get; set; } = 1;
    public string GlAccountCode { get; set; } = "111201"; // Associated GL Account
    public decimal OverdraftLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public SysBranch? Branch { get; set; }
    public SysCurrency? Currency { get; set; }
    public GlAccount? GlAccount { get; set; }
}

public sealed class CashRegister
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string RegisterType { get; set; } = "MAIN"; // MAIN, BRANCH, CASHIER, PETTY_CASH
    public string? CustodianName { get; set; } // Person in charge (أمين الصندوق / العهدة)
    public string GlAccountCode { get; set; } = "111101"; // Associated GL Account
    public long CurrencyId { get; set; } = 1;

    public decimal MinLimit { get; set; }
    public decimal MaxLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public SysBranch? Branch { get; set; }
    public SysCurrency? Currency { get; set; }
    public GlAccount? GlAccount { get; set; }
}

public sealed class BankReconciliation
{
    public long Id { get; set; }
    public long BankAccountId { get; set; }
    public long FiscalYearId { get; set; }
    public long FiscalPeriodId { get; set; }

    public DateTime StatementDate { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public decimal BookEndingBalance { get; set; }

    public decimal TotalReconciledAmount { get; set; }
    public decimal UnreconciledDifference { get; set; }

    /// <summary>
    /// Status: DRAFT, IN_PROGRESS, COMPLETED, APPROVED
    /// </summary>
    public string Status { get; set; } = "DRAFT";

    public string? Notes { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public BankAccount? BankAccount { get; set; }
    public SysFiscalYear? FiscalYear { get; set; }
    public GlFiscalPeriod? FiscalPeriod { get; set; }
    public ICollection<BankStatementLine> StatementLines { get; set; } = new List<BankStatementLine>();
}

public sealed class BankStatementLine
{
    public long Id { get; set; }
    public long ReconciliationId { get; set; }

    public DateTime TransactionDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? ReferenceNo { get; set; }
    public string? ChequeNo { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal Debit { get; set; }  // Withdrawals / charges from bank perspective
    public decimal Credit { get; set; } // Deposits / inflows from bank perspective
    public decimal Balance { get; set; }

    public bool IsReconciled { get; set; }
    public DateTime? ReconciledDate { get; set; }
    public long? MatchedVoucherDetailId { get; set; }

    public BankReconciliation? Reconciliation { get; set; }
    public GlVoucherDetail? MatchedVoucherDetail { get; set; }
}
