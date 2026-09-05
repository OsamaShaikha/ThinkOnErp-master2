using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class AccountStatementRepository : IAccountStatementRepository
{
    private readonly OracleDbContext _context;

    public AccountStatementRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<GlAccount?> GetAccountByCodeAsync(
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccount>> GetChildAccountsAsync(
        string parentAccountCode,
        CancellationToken cancellationToken = default)
    {
        // Recursively or pattern matching all child accounts starting with parentAccountCode
        return await _context.GlAccounts
            .AsNoTracking()
            .Where(a => a.AccountCode.StartsWith(parentAccountCode) || a.ParentAccountCode == parentAccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccount>> GetAllAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.GlAccounts
            .AsNoTracking()
            .OrderBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
        IReadOnlyList<string> accountCodes,
        DateTime beforeDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlVoucherDetails
            .AsNoTracking()
            .Where(d => accountCodes.Contains(d.AccountCode) && d.Header.VoucherDate < beforeDate);

        if (!includeUnposted)
        {
            // Status=3 is Posted
            query = query.Where(d => d.Header.Status == 3);
        }
        else
        {
            // Exclude Reversed (Status=4)
            query = query.Where(d => d.Header.Status != 4);
        }

        if (branchId.HasValue && branchId.Value > 0)
            query = query.Where(d => d.Header.BranchId == branchId.Value);

        if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
            query = query.Where(d => d.Header.FiscalYearId == fiscalYearId.Value);

        if (!string.IsNullOrWhiteSpace(costCenterCode))
            query = query.Where(d => d.CostCenterCode == costCenterCode);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalDebit = g.Sum(d => d.LocalDebit),
                TotalCredit = g.Sum(d => d.LocalCredit)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals != null
            ? (totals.TotalDebit, totals.TotalCredit)
            : (0m, 0m);
    }

    public async Task<IReadOnlyList<GlVoucherDetail>> GetPeriodDetailsAsync(
        IReadOnlyList<string> accountCodes,
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlVoucherDetails
            .AsNoTracking()
            .Include(d => d.Header)
            .Include(d => d.Account)
            .Include(d => d.CostCenter)
            .Where(d => accountCodes.Contains(d.AccountCode));

        if (!includeUnposted)
        {
            query = query.Where(d => d.Header.Status == 3);
        }
        else
        {
            query = query.Where(d => d.Header.Status != 4);
        }

        if (fromDate.HasValue)
            query = query.Where(d => d.Header.VoucherDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.Header.VoucherDate <= toDate.Value);

        if (branchId.HasValue && branchId.Value > 0)
            query = query.Where(d => d.Header.BranchId == branchId.Value);

        if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
            query = query.Where(d => d.Header.FiscalYearId == fiscalYearId.Value);

        if (!string.IsNullOrWhiteSpace(costCenterCode))
            query = query.Where(d => d.CostCenterCode == costCenterCode);

        return await query
            .OrderBy(d => d.Header.VoucherDate)
            .ThenBy(d => d.Header.VoucherNo)
            .ThenBy(d => d.LineSer)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountSummaryAggregate>> GetAccountsSummaryAggregatesAsync(
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.GlVoucherDetails
            .AsNoTracking();

        if (!includeUnposted)
            baseQuery = baseQuery.Where(d => d.Header.Status == 3);
        else
            baseQuery = baseQuery.Where(d => d.Header.Status != 4);

        if (branchId.HasValue && branchId.Value > 0)
            baseQuery = baseQuery.Where(d => d.Header.BranchId == branchId.Value);

        if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
            baseQuery = baseQuery.Where(d => d.Header.FiscalYearId == fiscalYearId.Value);

        if (!string.IsNullOrWhiteSpace(costCenterCode))
            baseQuery = baseQuery.Where(d => d.CostCenterCode == costCenterCode);

        // Period movements
        var periodQuery = baseQuery.AsQueryable();
        if (fromDate.HasValue)
            periodQuery = periodQuery.Where(d => d.Header.VoucherDate >= fromDate.Value);
        if (toDate.HasValue)
            periodQuery = periodQuery.Where(d => d.Header.VoucherDate <= toDate.Value);

        var periodAggregates = await periodQuery
            .GroupBy(d => d.AccountCode)
            .Select(g => new
            {
                AccountCode = g.Key,
                PeriodDebit = g.Sum(d => d.LocalDebit),
                PeriodCredit = g.Sum(d => d.LocalCredit)
            })
            .ToListAsync(cancellationToken);

        // Opening movements (before fromDate)
        var openingMap = new Dictionary<string, (decimal Debit, decimal Credit)>();
        if (fromDate.HasValue)
        {
            var openingQuery = baseQuery.Where(d => d.Header.VoucherDate < fromDate.Value);
            var openingAggregates = await openingQuery
                .GroupBy(d => d.AccountCode)
                .Select(g => new
                {
                    AccountCode = g.Key,
                    OpeningDebit = g.Sum(d => d.LocalDebit),
                    OpeningCredit = g.Sum(d => d.LocalCredit)
                })
                .ToListAsync(cancellationToken);

            foreach (var o in openingAggregates)
            {
                openingMap[o.AccountCode] = (o.OpeningDebit, o.OpeningCredit);
            }
        }

        var allAccountCodes = new HashSet<string>(periodAggregates.Select(p => p.AccountCode));
        foreach (var k in openingMap.Keys)
            allAccountCodes.Add(k);

        var result = new List<AccountSummaryAggregate>(allAccountCodes.Count);
        var periodMap = periodAggregates.ToDictionary(p => p.AccountCode);

        foreach (var code in allAccountCodes)
        {
            decimal opDebit = 0m;
            decimal opCredit = 0m;
            if (openingMap.TryGetValue(code, out var op))
            {
                opDebit = op.Debit;
                opCredit = op.Credit;
            }

            decimal pDebit = 0m;
            decimal pCredit = 0m;
            if (periodMap.TryGetValue(code, out var p))
            {
                pDebit = p.PeriodDebit;
                pCredit = p.PeriodCredit;
            }

            result.Add(new AccountSummaryAggregate(code, opDebit, opCredit, pDebit, pCredit));
        }

        return result;
    }

    public async Task<IReadOnlyList<GlVoucherType>> GetVoucherTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherTypes
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlCostCenter>> GetCostCentersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.GlCostCenters
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ThinkOnErp.Domain.Entities.Views.GlAccountStatementView>> GetStatementFromViewAsync(
        IReadOnlyList<string> accountCodes,
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlAccountStatementViews
            .AsNoTracking()
            .Where(v => accountCodes.Contains(v.AccountCode));

        if (!includeUnposted)
        {
            query = query.Where(v => v.VoucherStatus == 3);
        }
        else
        {
            query = query.Where(v => v.VoucherStatus != 4);
        }

        if (fromDate.HasValue)
            query = query.Where(v => v.VoucherDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(v => v.VoucherDate <= toDate.Value);

        if (branchId.HasValue && branchId.Value > 0)
            query = query.Where(v => v.BranchId == branchId.Value);

        if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
            query = query.Where(v => v.FiscalYearId == fiscalYearId.Value);

        if (!string.IsNullOrWhiteSpace(costCenterCode))
            query = query.Where(v => v.CostCenterCode == costCenterCode);

        return await query
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNo)
            .ThenBy(v => v.LineSer)
            .ToListAsync(cancellationToken);
    }
}
