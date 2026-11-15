using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollPostingVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var currencies = await db.SysCurrencies.AsNoTracking().ToListAsync();
        var codes = currencies.GroupBy(c => SalaryPaymentMetadata.Normalize(c.ShortNameEn))
            .Where(g => g.Key.Length > 0 && g.Count() == 1).ToDictionary(g => g.Single().Id, g => g.Key);
        var branches = await db.SysBranches.AsNoTracking().Where(b => b.IsActive && b.BaseCurrencyId != null).ToListAsync();
        var years = await db.SysFiscalYears.AsNoTracking().Where(y => y.IsActive && !y.IsClosed).OrderBy(y => y.Id).ToListAsync();
        report.Add($"Posting resolver fixture preflight: unique currencies={codes.Count}, active branches with base currency={branches.Count}, open years={years.Count}.");
        var year = years.FirstOrDefault(y => branches.Any(b => b.Id == y.BranchId && codes.ContainsKey(b.BaseCurrencyId!.Value)));
        if (year == null)
        {
            report.Add("Posting resolver fixture unavailable: no open year belongs to an active branch with a unique base currency. No fixture writes attempted.");
            throw new InvalidOperationException("Posting verification requires a configured base currency/open branch year.");
        }
        var branch = branches.Single(b => b.Id == year.BranchId);
        var account = await db.GlAccounts.AsNoTracking().Where(a => a.IsActive && a.AccountType == "DETAIL" &&
            (!a.IsBranchSpecific || db.GlAccountBranches.Any(l => l.AccountCode == a.AccountCode && l.BranchId == branch.Id && l.IsActive)))
            .Select(a => a.AccountCode).FirstOrDefaultAsync();
        if (account == null)
        {
            report.Add("Posting resolver fixture unavailable: no active detail account available for the selected branch. No fixture writes attempted.");
            throw new InvalidOperationException("Posting verification requires an available account.");
        }
        report.Add("Posting resolver fixture branch/year/account selected; preparing journal type/configuration.");
        var type = await db.GlVoucherTypes.AsNoTracking().Where(t => t.IsActive && t.Category == "JOURNAL").Select(t => t.TypeCode).FirstAsync();
        var originalConfiguration = await db.Set<PayrollPostingConfiguration>().AsNoTracking().SingleOrDefaultAsync(c => c.BranchId == branch.Id);
        var originalPeriods = await db.Set<GlFiscalPeriod>().AsNoTracking().Where(p => p.FiscalYearId == year.Id)
            .Select(p => new { p.Id, p.Status }).ToListAsync();
        var originalJson = System.Text.Json.JsonSerializer.Serialize(originalConfiguration);
        await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
        {
            try
            {
                var date = year.StartDate;
                var periods = await db.Set<GlFiscalPeriod>().Where(p => p.FiscalYearId == year.Id && p.StartDate <= date && p.EndDate >= date).ToListAsync();
                foreach (var period in periods) period.Status = "OPEN";
                if (periods.Count == 0)
                {
                    var numbers = await db.Set<GlFiscalPeriod>().Where(p => p.FiscalYearId == year.Id).Select(p => p.PeriodNumber).ToListAsync();
                    var period = new GlFiscalPeriod { FiscalYearId = year.Id, PeriodNumber = Enumerable.Range(1, 13).First(n => !numbers.Contains(n)),
                        StartDate = date, EndDate = date, PeriodNameLocal = "HR_VERIFY", PeriodNameEn = "HR_VERIFY", CreationUser = "HR_VERIFY" };
                    db.Add(period); periods.Add(period);
                }
                await db.SaveChangesAsync();
                var service = new HrPayrollPostingService(db);
                // Only the resolver is exercised: no payroll or GL voucher is created.
                await service.SaveAsync(new PayrollPostingConfiguration { BranchId = branch.Id, CurrencyId = branch.BaseCurrencyId!.Value,
                    VoucherType = type, SalaryExpense = account, EmployerSscExpense = account, SalaryPayable = account,
                    SscPayable = account, TaxPayable = account, DeductionsPayable = account });
                var context = await service.ResolveAsync(branch.Id, date);
                if (context.CurrencyCode != codes[branch.BaseCurrencyId.Value] || context.FiscalYearId != year.Id)
                    throw new InvalidOperationException("Posting currency/year resolution did not match the configured branch.");
                foreach (var status in new[] { "SOFT_CLOSE", "HARD_CLOSE" })
                {
                    periods[0].Status = status; await db.SaveChangesAsync();
                    var rejected = false;
                    try { await service.ResolveAsync(branch.Id, date); }
                    catch (InvalidOperationException e) when (e.Message.Contains("fiscal period")) { rejected = true; }
                    if (!rejected) throw new InvalidOperationException("Posting bypassed a closed fiscal period.");
                }
                report.Add("Oracle payroll posting resolver: unique ERP base currency/open year resolved; SOFT_CLOSE and HARD_CLOSE rejected. Existing account/type used only as transaction-local resolver inputs; no GL voucher, serial, payroll state or FX written.");
            }
            finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
        }
        var afterConfiguration = await db.Set<PayrollPostingConfiguration>().AsNoTracking().SingleOrDefaultAsync(c => c.BranchId == branch.Id);
        var afterPeriods = await db.Set<GlFiscalPeriod>().AsNoTracking().Where(p => p.FiscalYearId == year.Id)
            .Select(p => new { p.Id, p.Status }).ToListAsync();
        if (System.Text.Json.JsonSerializer.Serialize(afterConfiguration) != originalJson ||
            !originalPeriods.OrderBy(p => p.Id).SequenceEqual(afterPeriods.OrderBy(p => p.Id)))
            throw new InvalidOperationException("Payroll posting resolver fixture rollback failed.");
        report.Add("Payroll posting resolver rollback verified: original posting configuration and all fiscal period IDs/statuses restored. Not an Oracle GL posting/reversal or concurrency test.");
    }
}
