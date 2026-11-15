using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class HrPayrollPostingService(OracleDbContext db) : IHrPayrollPostingService
{
    public Task<PayrollPostingConfiguration?> GetAsync(long branchId, CancellationToken cancellationToken = default) =>
        db.Set<PayrollPostingConfiguration>().SingleOrDefaultAsync(c => c.BranchId == branchId, cancellationToken);

    private async Task<string> Validate(PayrollPostingConfiguration configuration, CancellationToken cancellation)
    {
        var branch = await db.SysBranches.SingleOrDefaultAsync(b => b.Id == configuration.BranchId && b.IsActive, cancellation)
            ?? throw new ArgumentException("Branch does not exist or is inactive.");
        var currency = await db.SysCurrencies.SingleOrDefaultAsync(c => c.Id == configuration.CurrencyId, cancellation)
            ?? throw new ArgumentException("Currency does not exist.");
        var currencyCode = SalaryPaymentMetadata.Normalize(currency.ShortNameEn);
        if (currencyCode.Length == 0 || await db.SysCurrencies.CountAsync(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == currencyCode, cancellation) != 1)
            throw new ArgumentException("Payroll posting requires a unique, non-empty ERP currency code.");
        if (branch.BaseCurrencyId != configuration.CurrencyId)
            throw new ArgumentException("Payroll posting currency must match the branch base currency; foreign-currency conversion is not configured.");
        await SerialPolicyAsync(configuration.VoucherType, cancellation);
        var requiredAccounts = new[] { configuration.SalaryExpense, configuration.EmployerSscExpense, configuration.SalaryPayable,
            configuration.SscPayable, configuration.TaxPayable, configuration.DeductionsPayable };
        if (requiredAccounts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("All required payroll posting accounts must be configured.");
        var loanReceivableAccount = configuration.LoanReceivableAccount?.Trim();
        var advanceReceivableAccount = configuration.AdvanceReceivableAccount?.Trim();
        var requiredAccountCodes = requiredAccounts.Select(code => code!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(loanReceivableAccount) && requiredAccountCodes.Contains(loanReceivableAccount))
            throw new ArgumentException("The loan receivable account must be separate from the other payroll posting accounts.");
        if (!string.IsNullOrEmpty(advanceReceivableAccount) && requiredAccountCodes.Contains(advanceReceivableAccount))
            throw new ArgumentException("The advance receivable account must be separate from the other payroll posting accounts.");
        if (!string.IsNullOrEmpty(loanReceivableAccount) && !string.IsNullOrEmpty(advanceReceivableAccount) &&
            string.Equals(loanReceivableAccount, advanceReceivableAccount, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Loan and advance receivables must use separate accounts.");
        var optionalAccounts = new[] { loanReceivableAccount, advanceReceivableAccount }
            .Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code!);
        foreach (var code in requiredAccounts.Concat(optionalAccounts))
        {
            // IsPostable is a computed, ignored EF property (AccountType == DETAIL && IsActive).
            // Keep its two mapped predicates in SQL instead of asking EF to translate it.
            var account = await db.GlAccounts.SingleOrDefaultAsync(a => a.AccountCode == code && a.IsActive && a.AccountType == "DETAIL", cancellation);
            if (account == null) throw new ArgumentException($"Account '{code}' is not an active posting account.");
            if (account.IsControlAccount)
                throw new ArgumentException($"Account '{code}' is a control account and cannot receive an aggregated payroll line without party details.");
            if (account.IsBranchSpecific && !await db.GlAccountBranches.AnyAsync(b => b.AccountCode == code && b.BranchId == configuration.BranchId && b.IsActive, cancellation))
                throw new ArgumentException($"Account '{code}' is not assigned to this branch.");
        }
        return currencyCode;
    }
    public async Task SaveAsync(PayrollPostingConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.SalaryExpense = NormalizeRequiredAccount(configuration.SalaryExpense);
        configuration.EmployerSscExpense = NormalizeRequiredAccount(configuration.EmployerSscExpense);
        configuration.SalaryPayable = NormalizeRequiredAccount(configuration.SalaryPayable);
        configuration.SscPayable = NormalizeRequiredAccount(configuration.SscPayable);
        configuration.TaxPayable = NormalizeRequiredAccount(configuration.TaxPayable);
        configuration.DeductionsPayable = NormalizeRequiredAccount(configuration.DeductionsPayable);
        configuration.LoanReceivableAccount = NormalizeOptionalAccount(configuration.LoanReceivableAccount);
        configuration.AdvanceReceivableAccount = NormalizeOptionalAccount(configuration.AdvanceReceivableAccount);
        await Validate(configuration, cancellationToken);
        var existing = await GetAsync(configuration.BranchId, cancellationToken);
        if (existing == null) db.Add(configuration); else db.Entry(existing).CurrentValues.SetValues(configuration);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeRequiredAccount(string? accountCode) => accountCode?.Trim() ?? string.Empty;

    private static string? NormalizeOptionalAccount(string? accountCode)
    {
        var normalized = accountCode?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
    public async Task<PayrollPostingContext> ResolveAsync(long branchId, DateTime date, CancellationToken cancellationToken = default)
    {
        date = date.Date;
        var configuration = await GetAsync(branchId, cancellationToken) ?? throw new InvalidOperationException("Configure payroll posting accounts for this branch before posting.");
        var currencyCode = await Validate(configuration, cancellationToken);
        var yearId = await OpenYearAsync(branchId, date, cancellationToken);
        return new PayrollPostingContext(configuration, yearId, currencyCode, await SerialPolicyAsync(configuration.VoucherType, cancellationToken));
    }

    private async Task<string> SerialPolicyAsync(int voucherType, CancellationToken cancellation)
    {
        var type = await db.GlVoucherTypes.SingleOrDefaultAsync(v => v.TypeCode == voucherType && v.IsActive && v.Category == "JOURNAL", cancellation)
            ?? throw new ArgumentException("An active journal voucher type is required.");
        var policy = SalaryPaymentMetadata.Normalize(type.SerialResetPolicy);
        if (policy is not ("MONTHLY" or "YEARLY" or "CONTINUOUS"))
            throw new ArgumentException("Configure a supported serial reset policy for the journal voucher type.");
        return policy;
    }

    public async Task<PayrollReversalContext> ResolveReversalAsync(long branchId, DateTime date, int voucherType, CancellationToken cancellationToken = default)
    {
        if (!await db.SysBranches.AnyAsync(b => b.Id == branchId && b.IsActive, cancellationToken))
            throw new ArgumentException("Branch does not exist or is inactive.");
        return new PayrollReversalContext(await OpenYearAsync(branchId, date.Date, cancellationToken), await SerialPolicyAsync(voucherType, cancellationToken));
    }

    private async Task<long> OpenYearAsync(long branchId, DateTime date, CancellationToken cancellationToken)
    {
        var year = await db.SysFiscalYears.SingleOrDefaultAsync(y => y.BranchId == branchId && y.IsActive && !y.IsClosed && y.StartDate <= date && y.EndDate >= date, cancellationToken)
            ?? throw new InvalidOperationException("An open fiscal year covering the payroll posting date is required.");
        // Match ERP voucher posting: a configured closed period cannot be bypassed
        // by creating the HR voucher directly through the repository.
        if (await db.Set<GlFiscalPeriod>().AnyAsync(p =>
                p.FiscalYearId == year.Id && p.StartDate <= date && p.EndDate >= date && p.Status != "OPEN", cancellationToken))
            throw new InvalidOperationException("The ERP fiscal period is not open for payroll posting.");
        return year.Id;
    }
}
