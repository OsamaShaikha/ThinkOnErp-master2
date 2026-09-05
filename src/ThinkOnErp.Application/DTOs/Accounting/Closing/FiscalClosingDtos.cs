namespace ThinkOnErp.Application.DTOs.Accounting.Closing;

public sealed class PreClosingPeriodValidationDto
{
    public long FiscalPeriodId { get; set; }
    public int PeriodNumber { get; set; }
    public string PeriodNameLocal { get; set; } = string.Empty;
    public bool IsReadyToClose { get; set; }
    public int UnpostedDraftVouchersCount { get; set; }
    public int UnderReviewVouchersCount { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
}

public sealed class PreClosingYearValidationDto
{
    public long FiscalYearId { get; set; }
    public int YearNumber { get; set; }
    public bool IsReadyToClose { get; set; }
    public int UnclosedPeriodsCount { get; set; }
    public int TotalUnpostedVouchersCount { get; set; }
    public decimal TrialBalanceDebit { get; set; }
    public decimal TrialBalanceCredit { get; set; }
    public decimal TrialBalanceDifference { get; set; }
    public bool IsTrialBalanceBalanced => Math.Abs(TrialBalanceDifference) < 0.001m;
    public decimal ProjectedNetIncome { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
}

public sealed class ExecutePeriodCloseRequest
{
    public string? Reason { get; set; }
}

public sealed class ExecutePeriodReopenRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ExecuteYearEndCloseRequest
{
    public string RetainedEarningsAccountCode { get; set; } = "310301"; // الأرباح المبقاة
    public long? TargetNextFiscalYearId { get; set; }
    public bool GenerateOpeningVoucher { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class YearEndClosingResultDto
{
    public long FiscalYearId { get; set; }
    public int YearNumber { get; set; }
    public long ClosingVoucherId { get; set; }
    public string ClosingVoucherNo { get; set; } = string.Empty;
    public decimal TotalRevenues { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome { get; set; }
    public string RetainedEarningsAccountCode { get; set; } = string.Empty;

    public long? OpeningVoucherId { get; set; }
    public string? OpeningVoucherNo { get; set; }
    public int RolledAccountsCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
