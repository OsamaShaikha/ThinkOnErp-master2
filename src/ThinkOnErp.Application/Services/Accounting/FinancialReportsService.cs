using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Reports;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class FinancialReportsService : IFinancialReportsService
{
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlAccountBalanceRepository _balanceRepository;
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly ISubledgerService _subledgerService;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<FinancialReportsService> _logger;

    public FinancialReportsService(
        IGlAccountRepository accountRepository,
        IGlAccountBalanceRepository balanceRepository,
        IGlVoucherRepository voucherRepository,
        ISubledgerService subledgerService,
        ICurrentTenantContext tenantContext,
        ILogger<FinancialReportsService> logger)
    {
        _accountRepository = accountRepository;
        _balanceRepository = balanceRepository;
        _voucherRepository = voucherRepository;
        _subledgerService = subledgerService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<GlStatementDto> GetGeneralLedgerStatementAsync(GlStatementFilterDto filter, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _accountRepository.GetByCodeAsync(companyId, filter.AccountCode, cancellationToken);
        if (account == null)
        {
            throw new AccountingNotFoundException($"الحساب برمز ({filter.AccountCode}) غير موجود.", "ACCOUNT_NOT_FOUND");
        }

        // Fetch posted vouchers up to ToDate
        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: filter.BranchId,
            year: null,
            month: null,
            typeCode: null,
            status: 3, // Posted
            fromDate: null,
            toDate: filter.ToDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var isDebitNature = account.NormalBalance == "DEBIT" || string.IsNullOrWhiteSpace(account.NormalBalance);

        // Opening transactions (before FromDate)
        var openingLines = vouchers
            .Where(v => v.VoucherDate < filter.FromDate.Date)
            .SelectMany(v => v.Details)
            .Where(d => d.AccountCode == filter.AccountCode)
            .ToList();

        var openingDebit = openingLines.Sum(d => d.LocalDebit);
        var openingCredit = openingLines.Sum(d => d.LocalCredit);
        var openingBalance = isDebitNature ? (openingDebit - openingCredit) : (openingCredit - openingDebit);

        // Period transactions (between FromDate and ToDate)
        var periodVouchers = vouchers
            .Where(v => v.VoucherDate >= filter.FromDate.Date && v.VoucherDate <= filter.ToDate.Date.AddDays(1).AddTicks(-1))
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNo)
            .ToList();

        var rows = new List<GlStatementRowDto>();
        var running = openingBalance;

        foreach (var v in periodVouchers)
        {
            var matchingDetails = v.Details.Where(d => d.AccountCode == filter.AccountCode);
            if (!string.IsNullOrWhiteSpace(filter.CostCenterCode))
            {
                matchingDetails = matchingDetails.Where(d => d.CostCenterCode == filter.CostCenterCode);
            }

            foreach (var d in matchingDetails)
            {
                if (isDebitNature)
                {
                    running += (d.LocalDebit - d.LocalCredit);
                }
                else
                {
                    running += (d.LocalCredit - d.LocalDebit);
                }

                rows.Add(new GlStatementRowDto
                {
                    VoucherId = v.Id,
                    VoucherNo = v.VoucherNo,
                    VoucherType = v.VoucherType,
                    VoucherTypeName = v.VoucherType switch { 1 => "قيد يومية", 2 => "سند قبض", 3 => "سند صرف", _ => "قيد محاسبي" },
                    VoucherDate = v.VoucherDate,
                    Description = d.Description ?? v.Description,
                    CostCenterCode = d.CostCenterCode,
                    PartyType = d.PartyType,
                    PartyCode = d.PartyCode,
                    Debit = d.LocalDebit,
                    Credit = d.LocalCredit,
                    RunningBalance = running
                });
            }
        }

        var totalDebit = rows.Sum(r => r.Debit);
        var totalCredit = rows.Sum(r => r.Credit);
        var netChange = isDebitNature ? (totalDebit - totalCredit) : (totalCredit - totalDebit);

        string category = account.AccountCode.StartsWith("1") ? "ASSET"
            : account.AccountCode.StartsWith("2") ? "LIABILITY"
            : account.AccountCode.StartsWith("3") ? "EQUITY"
            : account.AccountCode.StartsWith("4") ? "REVENUE"
            : "EXPENSE";

        return new GlStatementDto
        {
            AccountCode = account.AccountCode,
            AccountNameAr = account.AccountNameAr,
            AccountNameEn = account.AccountNameEn,
            AccountCategory = category,
            Nature = account.NormalBalance ?? "DEBIT",
            BranchId = filter.BranchId,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            OpeningBalance = openingBalance,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            NetPeriodChange = netChange,
            ClosingBalance = running,
            Rows = rows
        };
    }

    public async Task<IncomeStatementDto> GetIncomeStatementAsync(FinancialStatementFilterDto filter, CancellationToken cancellationToken = default)
    {
        var from = filter.FromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var to = filter.ToDate ?? DateTime.UtcNow;

        var companyId = _tenantContext.GetRequiredCompanyId();
        var allAccounts = await _accountRepository.GetAllAsync(companyId, cancellationToken);
        var accDict = allAccounts.ToDictionary(a => a.AccountCode);

        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: filter.BranchId,
            year: null,
            month: null,
            typeCode: null,
            status: 3, // Posted
            fromDate: from,
            toDate: to,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var lines = vouchers.SelectMany(v => v.Details).ToList();

        // 1. Operating Revenues (41... or Category REVENUE) -> Credit - Debit
        var revLines = lines.Where(l => l.AccountCode.StartsWith("41") || l.AccountCode.StartsWith("40"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalCredit - x.LocalDebit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 2. Cost of Goods Sold (51... or COGS) -> Debit - Credit
        var cogsLines = lines.Where(l => l.AccountCode.StartsWith("51"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalDebit - x.LocalCredit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 3. Operating Expenses (52... to 57...) -> Debit - Credit
        var opexLines = lines.Where(l => l.AccountCode.StartsWith("52") || l.AccountCode.StartsWith("53") || l.AccountCode.StartsWith("54") || l.AccountCode.StartsWith("55") || l.AccountCode.StartsWith("56") || l.AccountCode.StartsWith("57"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalDebit - x.LocalCredit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 4. Other Revenues (42... to 49...) -> Credit - Debit
        var otherRevLines = lines.Where(l => (l.AccountCode.StartsWith("4") && !l.AccountCode.StartsWith("41") && !l.AccountCode.StartsWith("40")))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalCredit - x.LocalDebit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 5. Other Expenses (58..., 59...) -> Debit - Credit
        var otherExpLines = lines.Where(l => l.AccountCode.StartsWith("58") || l.AccountCode.StartsWith("59"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalDebit - x.LocalCredit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        var totalRev = revLines.Sum(r => r.Amount);
        var totalCogs = cogsLines.Sum(r => r.Amount);
        var grossProfit = totalRev - totalCogs;

        var totalOpex = opexLines.Sum(r => r.Amount);
        var opProfit = grossProfit - totalOpex;

        var totalOtherRev = otherRevLines.Sum(r => r.Amount);
        var totalOtherExp = otherExpLines.Sum(r => r.Amount);

        var netIncome = opProfit + totalOtherRev - totalOtherExp;

        return new IncomeStatementDto
        {
            BranchId = filter.BranchId,
            FromDate = from,
            ToDate = to,
            Revenues = revLines,
            TotalRevenues = totalRev,
            CostOfGoodsSold = cogsLines,
            TotalCostOfGoodsSold = totalCogs,
            GrossProfit = grossProfit,
            OperatingExpenses = opexLines,
            TotalOperatingExpenses = totalOpex,
            OperatingProfit = opProfit,
            OtherRevenues = otherRevLines,
            TotalOtherRevenues = totalOtherRev,
            OtherExpenses = otherExpLines,
            TotalOtherExpenses = totalOtherExp,
            NetIncome = netIncome
        };
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(FinancialStatementFilterDto filter, CancellationToken cancellationToken = default)
    {
        var asOf = filter.AsOfDate ?? DateTime.UtcNow;

        var companyId = _tenantContext.GetRequiredCompanyId();
        var allAccounts = await _accountRepository.GetAllAsync(companyId, cancellationToken);
        var accDict = allAccounts.ToDictionary(a => a.AccountCode);

        // Fetch all posted vouchers up to AsOfDate
        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: filter.BranchId,
            year: null,
            month: null,
            typeCode: null,
            status: 3, // Posted
            fromDate: null,
            toDate: asOf,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var lines = vouchers.SelectMany(v => v.Details).ToList();

        // 1. Current Assets (11...) -> Debit - Credit
        var currentAssets = lines.Where(l => l.AccountCode.StartsWith("11"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalDebit - x.LocalCredit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 2. Non-Current Assets (12..., 13..., 14...) -> Debit - Credit
        var nonCurrentAssets = lines.Where(l => l.AccountCode.StartsWith("1") && !l.AccountCode.StartsWith("11"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalDebit - x.LocalCredit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 3. Current Liabilities (21...) -> Credit - Debit
        var currentLiabilities = lines.Where(l => l.AccountCode.StartsWith("21"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalCredit - x.LocalDebit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 4. Non-Current Liabilities (22... to 29...) -> Credit - Debit
        var nonCurrentLiabilities = lines.Where(l => l.AccountCode.StartsWith("2") && !l.AccountCode.StartsWith("21"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalCredit - x.LocalDebit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 5. Equity Items (31... to 39...) -> Credit - Debit
        var equityItems = lines.Where(l => l.AccountCode.StartsWith("3"))
            .GroupBy(l => l.AccountCode)
            .Select(g =>
            {
                accDict.TryGetValue(g.Key, out var acc);
                var amount = g.Sum(x => x.LocalCredit - x.LocalDebit);
                return new ReportAccountLineDto
                {
                    AccountCode = g.Key,
                    AccountNameAr = acc?.AccountNameAr ?? g.Key,
                    AccountNameEn = acc?.AccountNameEn ?? g.Key,
                    Amount = amount
                };
            })
            .Where(r => r.Amount != 0)
            .ToList();

        // 6. Current Period Net Income (Revenues 4... - Expenses 5...)
        var totalRev = lines.Where(l => l.AccountCode.StartsWith("4")).Sum(l => l.LocalCredit - l.LocalDebit);
        var totalExp = lines.Where(l => l.AccountCode.StartsWith("5")).Sum(l => l.LocalDebit - l.LocalCredit);
        var currentNetIncome = totalRev - totalExp;

        var totalCurrAssets = currentAssets.Sum(a => a.Amount);
        var totalNonCurrAssets = nonCurrentAssets.Sum(a => a.Amount);
        var totalAssets = totalCurrAssets + totalNonCurrAssets;

        var totalCurrLiab = currentLiabilities.Sum(l => l.Amount);
        var totalNonCurrLiab = nonCurrentLiabilities.Sum(l => l.Amount);
        var totalLiab = totalCurrLiab + totalNonCurrLiab;

        var totalEquity = equityItems.Sum(e => e.Amount) + currentNetIncome;
        var totalLiabAndEquity = totalLiab + totalEquity;

        return new BalanceSheetDto
        {
            BranchId = filter.BranchId,
            AsOfDate = asOf,
            CurrentAssets = currentAssets,
            TotalCurrentAssets = totalCurrAssets,
            NonCurrentAssets = nonCurrentAssets,
            TotalNonCurrentAssets = totalNonCurrAssets,
            TotalAssets = totalAssets,
            CurrentLiabilities = currentLiabilities,
            TotalCurrentLiabilities = totalCurrLiab,
            NonCurrentLiabilities = nonCurrentLiabilities,
            TotalNonCurrentLiabilities = totalNonCurrLiab,
            TotalLiabilities = totalLiab,
            EquityItems = equityItems,
            CurrentPeriodNetIncome = currentNetIncome,
            TotalEquity = totalEquity,
            TotalLiabilitiesAndEquity = totalLiabAndEquity
        };
    }

    public async Task<StatementOfAccountDto> GetCustomerStatementAsync(string customerCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _subledgerService.GetCustomerStatementAsync(customerCode, fromDate, toDate, cancellationToken);
    }

    public async Task<StatementOfAccountDto> GetVendorStatementAsync(string vendorCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _subledgerService.GetVendorStatementAsync(vendorCode, fromDate, toDate, cancellationToken);
    }

    public async Task<AgingReportDto> GetArAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        return await _subledgerService.GetArAgingReportAsync(asOfDate, cancellationToken);
    }

    public async Task<AgingReportDto> GetApAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        return await _subledgerService.GetApAgingReportAsync(asOfDate, cancellationToken);
    }
}
