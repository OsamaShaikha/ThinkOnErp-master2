using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Balances;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlAccountBalanceService : IGlAccountBalanceService
{
    private readonly IGlAccountBalanceRepository _balanceRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<GlAccountBalanceService> _logger;

    public GlAccountBalanceService(
        IGlAccountBalanceRepository balanceRepository,
        IGlAccountRepository accountRepository,
        IFiscalYearRepository fiscalYearRepository,
        ICurrentTenantContext tenantContext,
        ILogger<GlAccountBalanceService> logger)
    {
        _balanceRepository = balanceRepository;
        _accountRepository = accountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlAccountBalanceDto>> GetBalancesAsync(
        AccountBalanceFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var balances = await _balanceRepository.GetBalancesAsync(
            filter.AccountCode,
            filter.BranchId,
            filter.FiscalYearId,
            filter.FiscalPeriodId,
            cancellationToken);

        return balances.Select(b => new GlAccountBalanceDto
        {
            Id = b.Id,
            AccountCode = b.AccountCode,
            AccountNameAr = b.Account?.AccountNameAr ?? string.Empty,
            AccountNameEn = b.Account?.AccountNameEn ?? string.Empty,
            BranchId = b.BranchId,
            FiscalYearId = b.FiscalYearId,
            FiscalPeriodId = b.FiscalPeriodId,
            PeriodNameAr = b.FiscalPeriod?.PeriodNameAr,
            PeriodNameEn = b.FiscalPeriod?.PeriodNameEn,
            CurrencyId = b.CurrencyId,
            CurrencyCode = b.Currency?.ShortNameEn,
            OpeningDebit = b.OpeningDebit,
            OpeningCredit = b.OpeningCredit,
            PeriodDebit = b.PeriodDebit,
            PeriodCredit = b.PeriodCredit,
            ClosingDebit = b.ClosingDebit,
            ClosingCredit = b.ClosingCredit,
            LocalOpeningDebit = b.LocalOpeningDebit,
            LocalOpeningCredit = b.LocalOpeningCredit,
            LocalPeriodDebit = b.LocalPeriodDebit,
            LocalPeriodCredit = b.LocalPeriodCredit,
            LocalClosingDebit = b.LocalClosingDebit,
            LocalClosingCredit = b.LocalClosingCredit
        }).ToList();
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(
        long fiscalYearId,
        long fromPeriodId,
        long toPeriodId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var allAccounts = await _accountRepository.GetAllAsync(companyId, cancellationToken);
        var balances = await _balanceRepository.GetTrialBalanceAsync(branchId, fiscalYearId, fromPeriodId, toPeriodId, cancellationToken);

        var balanceDict = balances.GroupBy(b => b.AccountCode).ToDictionary(
            g => g.Key,
            g => new
            {
                LocalOpeningDebit = g.Sum(x => x.LocalOpeningDebit),
                LocalOpeningCredit = g.Sum(x => x.LocalOpeningCredit),
                LocalPeriodDebit = g.Sum(x => x.LocalPeriodDebit),
                LocalPeriodCredit = g.Sum(x => x.LocalPeriodCredit),
                LocalClosingDebit = g.Sum(x => x.LocalClosingDebit),
                LocalClosingCredit = g.Sum(x => x.LocalClosingCredit)
            });

        var rows = new List<TrialBalanceRowDto>();

        foreach (var acc in allAccounts.OrderBy(a => a.AccountCode))
        {
            balanceDict.TryGetValue(acc.AccountCode, out var b);

            var opDebit = b?.LocalOpeningDebit ?? 0;
            var opCredit = b?.LocalOpeningCredit ?? 0;
            var pDebit = b?.LocalPeriodDebit ?? 0;
            var pCredit = b?.LocalPeriodCredit ?? 0;
            var clDebit = b?.LocalClosingDebit ?? 0;
            var clCredit = b?.LocalClosingCredit ?? 0;

            rows.Add(new TrialBalanceRowDto
            {
                AccountCode = acc.AccountCode,
                AccountNameAr = acc.AccountNameAr,
                AccountNameEn = acc.AccountNameEn,
                AccountLevel = acc.AccountLevel,
                AccountType = acc.AccountType,
                ParentAccountCode = acc.ParentAccountCode,
                OpeningDebit = opDebit,
                OpeningCredit = opCredit,
                PeriodDebit = pDebit,
                PeriodCredit = pCredit,
                EndingDebit = clDebit,
                EndingCredit = clCredit
            });
        }

        return new TrialBalanceReportDto
        {
            FiscalYearId = fiscalYearId,
            BranchId = branchId,
            FromPeriodId = fromPeriodId,
            ToPeriodId = toPeriodId,
            TotalOpeningDebit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.OpeningDebit),
            TotalOpeningCredit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.OpeningCredit),
            TotalPeriodDebit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.PeriodDebit),
            TotalPeriodCredit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.PeriodCredit),
            TotalEndingDebit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.EndingDebit),
            TotalEndingCredit = rows.Where(r => r.AccountType == "DETAIL").Sum(r => r.EndingCredit),
            Rows = rows
        };
    }

    public async Task RecalculateBalancesAsync(long fiscalYearId, string username, CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _fiscalYearRepository.GetByIdAsync(fiscalYearId);
        if (fiscalYear == null)
        {
            throw new AccountingNotFoundException($"السنة المالية رقم ({fiscalYearId}) غير موجودة.", "FISCAL_YEAR_NOT_FOUND");
        }

        _logger.LogInformation("Recalculating account balances for fiscal year {FiscalYearId} by user {User}", fiscalYearId, username);
        await _balanceRepository.RecalculateAllBalancesAsync(fiscalYearId, username, cancellationToken);
    }
}
