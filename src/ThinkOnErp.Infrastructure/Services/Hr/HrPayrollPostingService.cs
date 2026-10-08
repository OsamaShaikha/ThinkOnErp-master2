using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class HrPayrollPostingService(OracleDbContext db) : IHrPayrollPostingService
{
    public Task<PayrollPostingConfiguration?> GetAsync(long branchId, CancellationToken cancellationToken = default) =>
        db.Set<PayrollPostingConfiguration>().SingleOrDefaultAsync(c => c.BranchId == branchId, cancellationToken);

    private async Task Validate(PayrollPostingConfiguration configuration, CancellationToken cancellation)
    {
        var branch = await db.SysBranches.SingleOrDefaultAsync(b => b.Id == configuration.BranchId && b.IsActive, cancellation)
            ?? throw new ArgumentException("Branch does not exist or is inactive.");
        if (!await db.SysCurrencies.AnyAsync(c => c.Id == configuration.CurrencyId, cancellation)) throw new ArgumentException("Currency does not exist.");
        if (branch.BaseCurrencyId != configuration.CurrencyId)
            throw new ArgumentException("Payroll posting currency must match the branch base currency; foreign-currency conversion is not configured.");
        if (!await db.GlVoucherTypes.AnyAsync(v => v.TypeCode == configuration.VoucherType && v.IsActive && v.Category == "JOURNAL", cancellation))
            throw new ArgumentException("An active journal voucher type is required.");
        foreach (var code in new[] { configuration.SalaryExpense, configuration.EmployerSscExpense, configuration.SalaryPayable,
            configuration.SscPayable, configuration.TaxPayable, configuration.DeductionsPayable })
        {
            var account = await db.GlAccounts.SingleOrDefaultAsync(a => a.AccountCode == code && a.IsActive && a.AccountType == "DETAIL", cancellation);
            if (account == null) throw new ArgumentException($"Account '{code}' is not an active posting account.");
            if (account.IsBranchSpecific && !await db.GlAccountBranches.AnyAsync(b => b.AccountCode == code && b.BranchId == configuration.BranchId && b.IsActive, cancellation))
                throw new ArgumentException($"Account '{code}' is not assigned to this branch.");
        }
    }
    public async Task SaveAsync(PayrollPostingConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await Validate(configuration, cancellationToken);
        var existing = await GetAsync(configuration.BranchId, cancellationToken);
        if (existing == null) db.Add(configuration); else db.Entry(existing).CurrentValues.SetValues(configuration);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<PayrollPostingContext> ResolveAsync(long branchId, DateTime date, CancellationToken cancellationToken = default)
    {
        var configuration = await GetAsync(branchId, cancellationToken) ?? throw new InvalidOperationException("Configure payroll posting accounts for this branch before posting.");
        await Validate(configuration, cancellationToken);
        var year = await db.SysFiscalYears.SingleOrDefaultAsync(y => y.BranchId == branchId && y.IsActive && !y.IsClosed && y.StartDate <= date && y.EndDate >= date, cancellationToken)
            ?? throw new InvalidOperationException("An open fiscal year covering the payroll posting date is required.");
        return new PayrollPostingContext(configuration, year.Id);
    }
}
