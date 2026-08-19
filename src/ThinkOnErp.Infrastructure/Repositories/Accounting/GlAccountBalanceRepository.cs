using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlAccountBalanceRepository : IGlAccountBalanceRepository
{
    private readonly OracleDbContext _context;

    public GlAccountBalanceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<GlAccountBalance?> GetBalanceAsync(
        string accountCode,
        long branchId,
        long fiscalYearId,
        long fiscalPeriodId,
        long currencyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlAccountBalances
            .Include(b => b.Account)
            .Include(b => b.Currency)
            .Include(b => b.FiscalPeriod)
            .FirstOrDefaultAsync(b =>
                b.AccountCode == accountCode &&
                b.BranchId == branchId &&
                b.FiscalYearId == fiscalYearId &&
                b.FiscalPeriodId == fiscalPeriodId &&
                b.CurrencyId == currencyId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccountBalance>> GetBalancesAsync(
        string? accountCode,
        long? branchId,
        long fiscalYearId,
        long? fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlAccountBalances
            .Include(b => b.Account)
            .Include(b => b.Currency)
            .Include(b => b.FiscalPeriod)
            .Where(b => b.FiscalYearId == fiscalYearId);

        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            query = query.Where(b => b.AccountCode == accountCode);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(b => b.BranchId == branchId.Value);
        }

        if (fiscalPeriodId.HasValue && fiscalPeriodId.Value > 0)
        {
            query = query.Where(b => b.FiscalPeriodId == fiscalPeriodId.Value);
        }

        return await query.OrderBy(b => b.AccountCode).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccountBalance>> GetTrialBalanceAsync(
        long? branchId,
        long fiscalYearId,
        long fromPeriodId,
        long toPeriodId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlAccountBalances
            .Include(b => b.Account)
            .Include(b => b.Currency)
            .Include(b => b.FiscalPeriod)
            .Where(b => b.FiscalYearId == fiscalYearId &&
                        b.FiscalPeriodId >= fromPeriodId &&
                        b.FiscalPeriodId <= toPeriodId);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(b => b.BranchId == branchId.Value);
        }

        return await query.OrderBy(b => b.AccountCode).ToListAsync(cancellationToken);
    }

    public async Task UpsertBalanceAsync(GlAccountBalance balance, CancellationToken cancellationToken = default)
    {
        var existing = await _context.GlAccountBalances.FirstOrDefaultAsync(b =>
            b.AccountCode == balance.AccountCode &&
            b.BranchId == balance.BranchId &&
            b.FiscalYearId == balance.FiscalYearId &&
            b.FiscalPeriodId == balance.FiscalPeriodId &&
            b.CurrencyId == balance.CurrencyId,
            cancellationToken);

        if (existing == null)
        {
            await _context.GlAccountBalances.AddAsync(balance, cancellationToken);
        }
        else
        {
            existing.OpeningDebit = balance.OpeningDebit;
            existing.OpeningCredit = balance.OpeningCredit;
            existing.PeriodDebit = balance.PeriodDebit;
            existing.PeriodCredit = balance.PeriodCredit;
            existing.ClosingDebit = balance.ClosingDebit;
            existing.ClosingCredit = balance.ClosingCredit;

            existing.LocalOpeningDebit = balance.LocalOpeningDebit;
            existing.LocalOpeningCredit = balance.LocalOpeningCredit;
            existing.LocalPeriodDebit = balance.LocalPeriodDebit;
            existing.LocalPeriodCredit = balance.LocalPeriodCredit;
            existing.LocalClosingDebit = balance.LocalClosingDebit;
            existing.LocalClosingCredit = balance.LocalClosingCredit;

            existing.UpdateUser = balance.UpdateUser;
            existing.UpdateDate = DateTime.UtcNow;
        }
    }

    public async Task UpdateBalanceFromVoucherAsync(
        long fiscalYearId,
        long fiscalPeriodId,
        long branchId,
        string accountCode,
        long currencyId,
        decimal debit,
        decimal credit,
        decimal localDebit,
        decimal localCredit,
        bool isAddition,
        string username,
        CancellationToken cancellationToken = default)
    {
        var balance = await _context.GlAccountBalances.FirstOrDefaultAsync(b =>
            b.AccountCode == accountCode &&
            b.BranchId == branchId &&
            b.FiscalYearId == fiscalYearId &&
            b.FiscalPeriodId == fiscalPeriodId &&
            b.CurrencyId == currencyId,
            cancellationToken);

        var multiplier = isAddition ? 1m : -1m;

        if (balance == null)
        {
            balance = new GlAccountBalance
            {
                FiscalYearId = fiscalYearId,
                FiscalPeriodId = fiscalPeriodId,
                BranchId = branchId,
                AccountCode = accountCode,
                CurrencyId = currencyId,
                PeriodDebit = Math.Max(0, debit * multiplier),
                PeriodCredit = Math.Max(0, credit * multiplier),
                ClosingDebit = Math.Max(0, debit * multiplier),
                ClosingCredit = Math.Max(0, credit * multiplier),
                LocalPeriodDebit = Math.Max(0, localDebit * multiplier),
                LocalPeriodCredit = Math.Max(0, localCredit * multiplier),
                LocalClosingDebit = Math.Max(0, localDebit * multiplier),
                LocalClosingCredit = Math.Max(0, localCredit * multiplier),
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            };
            await _context.GlAccountBalances.AddAsync(balance, cancellationToken);
        }
        else
        {
            balance.PeriodDebit += debit * multiplier;
            balance.PeriodCredit += credit * multiplier;
            balance.LocalPeriodDebit += localDebit * multiplier;
            balance.LocalPeriodCredit += localCredit * multiplier;

            // Recalculate closing
            var net = (balance.OpeningDebit - balance.OpeningCredit) + (balance.PeriodDebit - balance.PeriodCredit);
            balance.ClosingDebit = net >= 0 ? net : 0;
            balance.ClosingCredit = net < 0 ? -net : 0;

            var localNet = (balance.LocalOpeningDebit - balance.LocalOpeningCredit) + (balance.LocalPeriodDebit - balance.LocalPeriodCredit);
            balance.LocalClosingDebit = localNet >= 0 ? localNet : 0;
            balance.LocalClosingCredit = localNet < 0 ? -localNet : 0;

            balance.UpdateUser = username;
            balance.UpdateDate = DateTime.UtcNow;
        }
    }

    public async Task RecalculateAllBalancesAsync(long fiscalYearId, string username, CancellationToken cancellationToken = default)
    {
        // 1. Clear existing period balances for the fiscal year
        var existingBalances = await _context.GlAccountBalances
            .Where(b => b.FiscalYearId == fiscalYearId)
            .ToListAsync(cancellationToken);

        _context.GlAccountBalances.RemoveRange(existingBalances);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Query all posted voucher details for the fiscal year grouped by dimensions
        var postedLines = await (from d in _context.GlVoucherDetails
                                 join h in _context.GlVoucherHeaders on d.VoucherId equals h.Id
                                 where h.FiscalYearId == fiscalYearId && h.Status == 3 // Posted only
                                 select new
                                 {
                                     h.FiscalYearId,
                                     BranchId = d.BranchId ?? h.BranchId,
                                     d.AccountCode,
                                     d.CurrencyId,
                                     h.VoucherDate,
                                     d.Debit,
                                     d.Credit,
                                     d.LocalDebit,
                                     d.LocalCredit
                                 }).ToListAsync(cancellationToken);

        var periods = await _context.GlFiscalPeriods
            .Where(p => p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(cancellationToken);

        var newBalances = new List<GlAccountBalance>();

        foreach (var period in periods)
        {
            var pLines = postedLines.Where(l => l.VoucherDate.Date >= period.StartDate.Date && l.VoucherDate.Date <= period.EndDate.Date);

            var grouped = pLines.GroupBy(l => new { l.BranchId, l.AccountCode, l.CurrencyId });

            foreach (var g in grouped)
            {
                var pDebit = g.Sum(x => x.Debit);
                var pCredit = g.Sum(x => x.Credit);
                var pLocalDebit = g.Sum(x => x.LocalDebit);
                var pLocalCredit = g.Sum(x => x.LocalCredit);

                var net = pDebit - pCredit;
                var localNet = pLocalDebit - pLocalCredit;

                newBalances.Add(new GlAccountBalance
                {
                    FiscalYearId = fiscalYearId,
                    FiscalPeriodId = period.Id,
                    BranchId = g.Key.BranchId,
                    AccountCode = g.Key.AccountCode,
                    CurrencyId = g.Key.CurrencyId,
                    PeriodDebit = pDebit,
                    PeriodCredit = pCredit,
                    ClosingDebit = net >= 0 ? net : 0,
                    ClosingCredit = net < 0 ? -net : 0,
                    LocalPeriodDebit = pLocalDebit,
                    LocalPeriodCredit = pLocalCredit,
                    LocalClosingDebit = localNet >= 0 ? localNet : 0,
                    LocalClosingCredit = localNet < 0 ? -localNet : 0,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        if (newBalances.Count > 0)
        {
            await _context.GlAccountBalances.AddRangeAsync(newBalances, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
