namespace ThinkOnErp.Application.DTOs.Accounting.Reports;

public sealed class GlStatementFilterDto
{
    public string AccountCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? CostCenterCode { get; set; }
}

public sealed class GlStatementRowDto
{
    public long VoucherId { get; set; }
    public long VoucherNo { get; set; }
    public int VoucherType { get; set; }
    public string VoucherTypeName { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string? Description { get; set; }
    public string? CostCenterCode { get; set; }
    public string? PartyType { get; set; }
    public string? PartyCode { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class GlStatementDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string AccountCategory { get; set; } = string.Empty;
    public string Nature { get; set; } = "DEBIT"; // DEBIT or CREDIT
    public long? BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetPeriodChange { get; set; }
    public decimal ClosingBalance { get; set; }

    public List<GlStatementRowDto> Rows { get; set; } = new();
}

public sealed class FinancialStatementFilterDto
{
    public long? BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public sealed class ReportAccountLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class IncomeStatementDto
{
    public string Title { get; set; } = "قائمة الدخل (الأرباح والخسائر)";
    public long? BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // 1. Operating Revenues
    public List<ReportAccountLineDto> Revenues { get; set; } = new();
    public decimal TotalRevenues { get; set; }

    // 2. Cost of Goods Sold / Cost of Sales
    public List<ReportAccountLineDto> CostOfGoodsSold { get; set; } = new();
    public decimal TotalCostOfGoodsSold { get; set; }

    // 3. Gross Profit (مجمل الربح) = Revenues - COGS
    public decimal GrossProfit { get; set; }

    // 4. Operating Expenses
    public List<ReportAccountLineDto> OperatingExpenses { get; set; } = new();
    public decimal TotalOperatingExpenses { get; set; }

    // 5. Operating Profit (الربح التشغيلي) = Gross Profit - Operating Expenses
    public decimal OperatingProfit { get; set; }

    // 6. Other Revenues & Expenses (Finance / Forex / Non-Operating)
    public List<ReportAccountLineDto> OtherRevenues { get; set; } = new();
    public decimal TotalOtherRevenues { get; set; }

    public List<ReportAccountLineDto> OtherExpenses { get; set; } = new();
    public decimal TotalOtherExpenses { get; set; }

    // 7. Net Profit / Loss (صافي الربح / الخسارة النهائي)
    public decimal NetIncome { get; set; }
    public bool IsProfitable => NetIncome >= 0;
}

public sealed class BalanceSheetDto
{
    public string Title { get; set; } = "الميزانية العمومية (قائمة المركز المالي)";
    public long? BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public DateTime AsOfDate { get; set; }

    // Assets
    public List<ReportAccountLineDto> CurrentAssets { get; set; } = new();
    public decimal TotalCurrentAssets { get; set; }

    public List<ReportAccountLineDto> NonCurrentAssets { get; set; } = new();
    public decimal TotalNonCurrentAssets { get; set; }

    public decimal TotalAssets { get; set; }

    // Liabilities
    public List<ReportAccountLineDto> CurrentLiabilities { get; set; } = new();
    public decimal TotalCurrentLiabilities { get; set; }

    public List<ReportAccountLineDto> NonCurrentLiabilities { get; set; } = new();
    public decimal TotalNonCurrentLiabilities { get; set; }

    public decimal TotalLiabilities { get; set; }

    // Equity
    public List<ReportAccountLineDto> EquityItems { get; set; } = new();
    public decimal CurrentPeriodNetIncome { get; set; } // dynamically computed from P&L
    public decimal TotalEquity { get; set; }

    // Total Liabilities + Equity
    public decimal TotalLiabilitiesAndEquity { get; set; }

    public decimal Variance => TotalAssets - TotalLiabilitiesAndEquity;
    public bool IsBalanced => Math.Abs(Variance) < 0.001m;
}
