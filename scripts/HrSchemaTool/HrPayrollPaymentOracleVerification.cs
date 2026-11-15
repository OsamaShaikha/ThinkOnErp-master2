using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollPaymentOracleVerification
{
    private const string Marker = "HR_PAYROLL_PAYMENT_VERIFY";

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        await using var db = new OracleDbContext(options);
        var now = DateTime.UtcNow;
        var date = now.Date;
        var candidateBranches = await db.SysBranches.AsNoTracking()
            .Where(b => b.IsActive && b.CompanyId.HasValue && b.BaseCurrencyId.HasValue)
            .Select(b => new { b.Id, b.CompanyId, b.BaseCurrencyId })
            .ToListAsync();
        var currencyIdsWithCode = await db.SysCurrencies.AsNoTracking()
            .Where(c => c.ShortNameEn != null).Select(c => c.Id).ToListAsync();
        var branchesWithCurrency = candidateBranches
            .Where(b => b.BaseCurrencyId.HasValue && currencyIdsWithCode.Contains(b.BaseCurrencyId.Value)).ToList();
        var candidateBranchIds = branchesWithCurrency.Select(b => b.Id).ToArray();
        var currentYears = await db.SysFiscalYears.AsNoTracking()
            .Where(y => candidateBranchIds.Contains(y.BranchId) && y.IsActive && !y.IsClosed &&
                y.StartDate <= date && y.EndDate >= date)
            .Select(y => new { y.Id, y.BranchId })
            .ToListAsync();
        var currentYearIds = currentYears.Select(y => y.Id).ToArray();
        var coveringPeriods = currentYearIds.Length == 0 ? 0 : await db.Set<GlFiscalPeriod>().AsNoTracking()
            .CountAsync(p => currentYearIds.Contains(p.FiscalYearId) && p.Status == "OPEN" &&
                p.StartDate <= date && p.EndDate >= date);
        report.Add($"INT-14 payment preflight (read-only): candidate active company branches={candidateBranches.Count}, branches with ERP currency code={branchesWithCurrency.Count}, active fiscal years covering today={currentYears.Count}, open GL periods covering today={coveringPeriods}.");
        var branch = await (from b in db.SysBranches.AsNoTracking()
            join y in db.SysFiscalYears.AsNoTracking() on b.Id equals y.BranchId
            join p in db.Set<GlFiscalPeriod>().AsNoTracking() on y.Id equals p.FiscalYearId
            where b.IsActive && b.CompanyId.HasValue && b.BaseCurrencyId.HasValue &&
                db.SysCurrencies.Any(c => c.Id == b.BaseCurrencyId && c.ShortNameEn != null) &&
                y.IsActive && !y.IsClosed && y.StartDate <= date && y.EndDate >= date && p.Status == "OPEN" &&
                p.StartDate <= date && p.EndDate >= date
            select new { b.Id, CompanyId = b.CompanyId!.Value, CurrencyId = b.BaseCurrencyId, FiscalYearId = y.Id })
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException("INT-14 Oracle payment verification requires an active company branch with an open fiscal period covering today.");
        if (await db.Set<PayrollPostingConfiguration>().IgnoreQueryFilters().AnyAsync(c => c.BranchId == branch.Id))
            throw new InvalidOperationException("INT-14 verifier refuses to replace an existing payroll posting configuration.");
        var currency = await db.SysCurrencies.AsNoTracking().Where(c => c.Id == branch.CurrencyId && c.ShortNameEn != null)
            .Select(c => new { c.Id, Code = c.ShortNameEn!.Trim() }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("INT-14 Oracle payment verification requires a branch base currency with an ERP currency code.");
        var journalType = await db.GlVoucherTypes.AsNoTracking().Where(t => t.IsActive && t.Category == "JOURNAL" &&
            (t.SerialResetPolicy == "MONTHLY" || t.SerialResetPolicy == "YEARLY" || t.SerialResetPolicy == "CONTINUOUS"))
            .OrderBy(t => t.TypeCode).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("INT-14 Oracle payment verification requires an active journal voucher type.");
        if (!await db.GlVoucherTypes.AsNoTracking().AnyAsync(t => t.TypeCode == 103 && t.IsActive && t.AllowManualEntry))
            throw new InvalidOperationException("INT-14 Oracle payment verification requires active, manually-enterable Payment Voucher type 103.");
        var accounts = (await new GlAccountRepository(db).GetPostableAsync(branch.CompanyId, branch.Id))
            .Where(a => !a.IsControlAccount && !string.IsNullOrWhiteSpace(a.AccountCode))
            .Select(a => a.AccountCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (accounts.Length < 2)
            throw new InvalidOperationException("INT-14 Oracle payment verification requires two active, branch-postable detail accounts.");

        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var paymentMethodCode = "HRPV" + token[..12];
        var sourceId = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var handoff = new HrAccountingHandoff
        {
            CompanyId = branch.CompanyId, BranchId = branch.Id, SourceType = "PAYROLL", SourceId = sourceId,
            SourceVersion = 1, Operation = "SUBMIT", PayPeriod = "ORACLE-VERIFY", SourceApprovedAtUtc = now,
            HandoffReference = "HR-PAYMENT-VERIFY-" + token[..10],
            PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))),
            DeliveryStatus = "DELIVERED", CreatedAtUtc = now, CreatedBy = Marker,
            Transitions = [new HrAccountingHandoffTransition { FromStatus = "INITIAL", ToStatus = "DELIVERED", Actor = Marker, OccurredAtUtc = now }],
            Lines = [
                new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = currency.Code, Amount = 1000m, BranchId = branch.Id },
                new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = currency.Code, Amount = 1000m, BranchId = branch.Id }
            ]
        };
        db.Set<PayrollPostingConfiguration>().Add(new PayrollPostingConfiguration
        {
            BranchId = branch.Id, CurrencyId = currency.Id, VoucherType = journalType.TypeCode,
            SalaryExpense = accounts[0], EmployerSscExpense = accounts[0], SalaryPayable = accounts[0],
            SscPayable = accounts[0], TaxPayable = accounts[0], DeductionsPayable = accounts[0]
        });
        db.PaymentMethods.Add(new PaymentMethod
        {
            BranchId = branch.Id, Code = paymentMethodCode, NameEn = "Temporary HR payment verification",
            NameLocal = "تحقق دفعات HR مؤقت", MethodType = "BANK", GlAccountCode = accounts[1],
            IsActive = true, ShowInVouchers = true, CreationUser = Marker, CreationDate = now
        });
        db.HrAccountingHandoffs.Add(handoff);
        await db.SaveChangesAsync();

        var journalRepository = new GlVoucherRepository(db);
        var gl = new GlVoucherService(journalRepository, new GlAccountRepository(db), new GlCostCenterRepository(db),
            new GlFiscalPeriodRepository(db), new GlAccountBalanceRepository(db), Unused<IArSubledgerRepository>(),
            Unused<IApSubledgerRepository>(), new Tenant(branch.CompanyId), NullLogger<GlVoucherService>.Instance);
        var paymentService = new ReceiptPaymentVoucherService(gl, journalRepository, Unused<ICustomerRepository>(),
            Unused<IVendorRepository>(), Unused<IArSubledgerRepository>(), Unused<IApSubledgerRepository>(),
            Unused<ISubledgerService>(), new Tenant(branch.CompanyId), NullLogger<ReceiptPaymentVoucherService>.Instance);
        var accounting = new HrAccountingHandoffService(db, journalRepository, new HrPayrollPostingService(db), paymentService, gl);
        var handoffId = handoff.Id;
        var paymentIds = new List<long>();
        var voucherIds = new HashSet<long>();
        Exception? failure = null;
        try
        {
            await accounting.ReviewAsync(handoffId, true, "Temporary Oracle verification", Marker);
            var journalId = await accounting.CreateJournalVoucherAsync(handoffId, Marker);
            voucherIds.Add(journalId);
            await gl.PostVoucherAsync(journalId, Marker);
            await accounting.ConfirmJournalPostedAsync(handoffId, journalId, "hr-payment-verify-jv-" + token, Marker);

            async Task<AccountingHandoffPayment> Prepare(decimal amount, string suffix)
            {
                var payment = await accounting.PreparePaymentAsync(handoffId, amount, currency.Code, paymentMethodCode,
                    "hr-payment-verify-" + token + "-" + suffix, Marker);
                paymentIds.Add(payment.Id);
                var linked = await journalRepository.GetBySourceAsync(branch.Id, "HR_PAYMENT", payment.Id);
                if (linked.Count != 1) throw new InvalidOperationException("Prepared payment must have exactly one ERP Payment Voucher.");
                voucherIds.Add(linked[0].Id);
                return payment;
            }
            async Task Post(AccountingHandoffPayment payment)
            {
                var linked = (await journalRepository.GetBySourceAsync(branch.Id, "HR_PAYMENT", payment.Id)).Single();
                await gl.PostVoucherAsync(linked.Id, Marker);
            }

            var first = await Prepare(400m, "partial");
            await Post(first);
            await accounting.ConfirmPaymentAsync(first.Id, "oracle-partial-400", "synthetic-verification-evidence", Marker);
            var failed = await Prepare(600m, "failed");
            await accounting.FailPaymentAsync(failed.Id, "Temporary verification failure", Marker);
            var failedVoucher = (await journalRepository.GetBySourceAsync(branch.Id, "HR_PAYMENT", failed.Id)).Single();
            if (failedVoucher.Status != 4) throw new InvalidOperationException("FailPaymentAsync did not void its draft ERP Payment Voucher.");
            var retry = await Prepare(600m, "retry");
            await Post(retry);
            await accounting.ConfirmPaymentAsync(retry.Id, "oracle-retry-600", "synthetic-verification-evidence", Marker);

            var reversalBlocked = false;
            try
            {
                await accounting.ReverseJournalVoucherAsync(handoffId, date, "Must be blocked until payments are reversed", Marker);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Void", StringComparison.OrdinalIgnoreCase)) { reversalBlocked = true; }
            if (!reversalBlocked)
                throw new InvalidOperationException("Journal reversal unexpectedly succeeded while confirmed payment instructions remained active.");

            foreach (var payment in new[] { retry, first })
                await accounting.ReversePaymentAsync(payment.Id, "Temporary Oracle lifecycle reversal", "oracle-reversal-" + payment.Id,
                    "synthetic-verification-evidence", Marker);
            var reversalId = await accounting.ReverseJournalVoucherAsync(handoffId, date, "Temporary Oracle lifecycle reversal", Marker);
            voucherIds.Add(reversalId);
            var finalPayments = await db.AccountingHandoffPayments.AsNoTracking().Where(p => p.HandoffId == handoffId).ToListAsync();
            if (finalPayments.Count != 3 || finalPayments.Count(p => p.Status == "REVERSED") != 2 || finalPayments.Count(p => p.Status == "FAILED") != 1)
                throw new InvalidOperationException("INT-14 final payment states do not match the partial/fail/retry/reverse lifecycle.");
            db.ChangeTracker.Clear();
            var finalHandoff = await db.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == handoffId);
            if (finalHandoff.DeliveryStatus != "REVERSED") throw new InvalidOperationException("INT-14 journal reversal did not mark the handoff REVERSED.");
            report.Add("INT-14 DEV_TEMPLATE Oracle payment lifecycle: actual HrAccountingHandoffService and ReceiptPaymentVoucherService prepared, posted, voided, retried, confirmed, and reversed payment vouchers; journal reversal was blocked while payments were confirmed and succeeded after both reversals. Synthetic posting configuration and method were temporary. Committed verification fixtures were removed in cleanup; not an approved customer mapping or production deployment check.");
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            try { await CleanupAsync(db, gl, handoffId, paymentIds, voucherIds, branch.Id, currency.Id,
                journalType.TypeCode, accounts[0], paymentMethodCode); }
            catch (Exception cleanupException)
            {
                throw new InvalidOperationException("INT-14 Oracle verification cleanup failed; inspect marked DEV_TEMPLATE fixtures before rerunning.",
                    failure == null ? cleanupException : new AggregateException(failure, cleanupException));
            }
        }
    }

    private static async Task CleanupAsync(OracleDbContext db, IGlVoucherService gl, long handoffId, IReadOnlyCollection<long> paymentIds,
        IReadOnlyCollection<long> voucherIds, long branchId, long currencyId, int journalType, string postingAccount,
        string paymentMethodCode)
    {
        var allVoucherIds = voucherIds.ToHashSet();
        var persistedPaymentIds = await db.AccountingHandoffPayments.AsNoTracking()
            .Where(p => p.HandoffId == handoffId).Select(p => p.Id).ToListAsync();
        foreach (var id in paymentIds) persistedPaymentIds.Add(id);
        foreach (var id in await db.GlVoucherHeaders.AsNoTracking()
            .Where(v => v.SourceSystemCode == "HR_HANDOFF" && v.SourceRefId == handoffId)
            .Select(v => v.Id).ToListAsync()) allVoucherIds.Add(id);
        foreach (var id in await db.GlVoucherHeaders.AsNoTracking()
            .Where(v => v.SourceSystemCode == "HR_PAYMENT" && v.SourceRefId.HasValue && persistedPaymentIds.Contains(v.SourceRefId.Value))
            .Select(v => v.Id).ToListAsync()) allVoucherIds.Add(id);
        foreach (var id in allVoucherIds.ToArray())
        {
            var voucher = await db.GlVoucherHeaders.Include(v => v.Details).SingleOrDefaultAsync(v => v.Id == id);
            if (voucher is { Status: 3, IsReversed: false } &&
                voucher.SourceSystemCode is "HR_HANDOFF" or "HR_PAYMENT")
                await gl.ReverseVoucherAsync(id, Marker, "Cleanup of temporary INT-14 Oracle verification", CancellationToken.None);
        }
        foreach (var id in voucherIds.ToArray())
        {
            foreach (var reverse in await db.GlVoucherHeaders.AsNoTracking().Where(v => v.SourceSystemCode == "GL_REVERSAL" && v.SourceRefId == id).Select(v => v.Id).ToListAsync())
                allVoucherIds.Add(reverse);
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID = {handoffId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID = {handoffId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_OUTBOX WHERE HANDOFF_ID = {handoffId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ACC_HANDOFF_INBOX WHERE HANDOFF_ID = {handoffId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ACC_HANDOFF_POSTING_RECEIPT WHERE HANDOFF_ID = {handoffId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ACC_HANDOFF_JOURNAL_LINK WHERE HANDOFF_ID = {handoffId}");
        foreach (var id in persistedPaymentIds.Distinct())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ACC_HANDOFF_PAYMENT WHERE ID = {id}");
        }
        var generatedReversalIds = await db.GlVoucherHeaders.AsNoTracking()
            .Where(v => v.SourceSystemCode == "GL_REVERSAL" && v.SourceRefId.HasValue && allVoucherIds.Contains(v.SourceRefId.Value))
            .Select(v => v.Id).ToListAsync();
        foreach (var id in generatedReversalIds) allVoucherIds.Add(id);
        foreach (var id in allVoucherIds)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM GL_VOUCHER_HEADER WHERE ID = {id}");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF WHERE ID = {handoffId}");
        // Do not remove a branch configuration that was replaced while this verifier was running.
        // The verifier owns only the exact temporary row it inserted.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_GL_POSTING_CONFIG WHERE BRANCH_ID = {branchId} AND CURRENCY_ID = {currencyId} AND VOUCHER_TYPE = {journalType} AND SALARY_EXPENSE = {postingAccount} AND EMPLOYER_SSC_EXPENSE = {postingAccount} AND SALARY_PAYABLE = {postingAccount} AND SSC_PAYABLE = {postingAccount} AND TAX_PAYABLE = {postingAccount} AND DEDUCTIONS_PAYABLE = {postingAccount} AND LOAN_RECEIVABLE_ACCOUNT IS NULL AND ADVANCE_RECEIVABLE_ACCOUNT IS NULL");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PAYMENT_METHOD WHERE BRANCH_ID = {branchId} AND CODE = {paymentMethodCode} AND CREATION_USER = {Marker}");
        await tx.CommitAsync();
    }

    private static T Unused<T>() where T : class => DispatchProxy.Create<T, UnexpectedProxy>();
    public class UnexpectedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected dependency call during Oracle payment verification: {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}.");
    }
    private sealed class Tenant(long companyId) : ICurrentTenantContext
    {
        public long GetRequiredCompanyId() => companyId;
        public string GetLanguageCode() => "en";
    }
}
