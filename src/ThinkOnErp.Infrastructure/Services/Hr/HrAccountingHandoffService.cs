using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class HrAccountingHandoffService(OracleDbContext db, IGlVoucherRepository vouchers, IHrPayrollPostingService payrollPosting, IReceiptPaymentVoucherService paymentVouchers, IGlVoucherService glVouchers, IHrUnitOfWork? unitOfWork = null) : IHrAccountingHandoffService
{
    public async Task<long> CreateJournalVoucherAsync(long handoffId, string user, CancellationToken cancellationToken = default)
    {
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff not found in active scope.");
        if (handoff.DeliveryStatus != "ACCEPTED") throw new InvalidOperationException("Only an Accounting-accepted handoff can create a journal voucher.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        var link = await db.AccountingHandoffJournalLinks.SingleOrDefaultAsync(x => x.HandoffId == handoff.Id, cancellationToken);
        if (link != null)
        {
            var linkedVoucher = await vouchers.GetByIdAsync(link.VoucherId, cancellationToken)
                ?? throw new InvalidOperationException("The saved payroll Journal Voucher link points to a missing Accounting voucher.");
            EnsureActivePayrollJournalLink(handoff, branchId, linkedVoucher);
            return link.VoucherId;
        }
        var existing = await vouchers.GetBySourceAsync(branchId, "HR_HANDOFF", handoff.Id, cancellationToken);
        if (existing.Count > 1)
            throw new InvalidOperationException("Multiple Accounting Journal Vouchers exist for this handoff; reconcile them before retrying creation.");
        if (existing.Count == 1)
        {
            EnsureActivePayrollJournalLink(handoff, branchId, existing[0]);
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink { HandoffId = handoff.Id, VoucherId = existing[0].Id, CreatedAtUtc = DateTime.UtcNow, CreatedBy = user });
            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);
            return existing[0].Id;
        }
        if (handoff.Lines.Count == 0)
            throw new InvalidOperationException("Payroll handoff has no posting lines.");
        if (handoff.Lines.Any(line => line.BranchId != branchId))
            throw new InvalidOperationException("Payroll handoff lines must all belong to the handoff branch.");
        var handoffCurrency = Normalize(handoff.Lines.First().CurrencyCode);
        if (handoffCurrency.Length == 0 || handoff.Lines.Any(line => Normalize(line.CurrencyCode) != handoffCurrency))
            throw new InvalidOperationException("Payroll handoff lines must all use the same explicit currency.");
        if (handoff.Lines.Any(line => line.LineType.StartsWith("DEDUCTION:", StringComparison.OrdinalIgnoreCase) && line.Amount < 0m))
            throw new InvalidOperationException("Payroll handoff cannot post a negative deduction component.");
        if (handoff.Lines.Any(line => line.Amount < 0m))
            throw new InvalidOperationException("Payroll handoff cannot post negative component, control, or settlement-reference amounts.");
        var date = handoff.CreatedAtUtc.Date;
        var context = await payrollPosting.ResolveAsync(branchId, date, cancellationToken);
        var config = context.Configuration;
        var currency = await db.SysCurrencies.SingleOrDefaultAsync(c => c.Id == config.CurrencyId && c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == handoffCurrency, cancellationToken)
            ?? throw new InvalidOperationException("Handoff currency does not match Accounting configuration.");
        if (handoffCurrency != context.CurrencyCode)
            throw new InvalidOperationException("Handoff currency does not match the resolved Accounting currency.");
        var gross = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:GROSS")).Sum(l => l.Amount);
        var employeeSsc = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:EMPLOYEE_SSC")).Sum(l => l.Amount);
        var employerSsc = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:EMPLOYER_SSC")).Sum(l => l.Amount);
        var tax = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:INCOME_TAX") || l.LineType.StartsWith("CONTROL:NATIONAL_CONTRIBUTION")).Sum(l => l.Amount);
        var deductions = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:OTHER_DEDUCTIONS")).Sum(l => l.Amount);
        var net = handoff.Lines.Where(l => l.LineType.StartsWith("CONTROL:NET_PAY")).Sum(l => l.Amount);
        if (decimal.Round(gross - employeeSsc - tax - deductions, 4) != net) throw new InvalidOperationException("Payroll snapshot does not balance to net pay.");
        var loanRepayments = handoff.Lines.Where(l => l.LineType == "REFERENCE:SCHEDULE").Sum(l => l.Amount);
        var advanceRecoveries = handoff.Lines.Where(l => l.LineType == "REFERENCE:ADVANCE").Sum(l => l.Amount);
        if (loanRepayments < 0m || advanceRecoveries < 0m || loanRepayments + advanceRecoveries > deductions)
            throw new InvalidOperationException("Payroll loan and advance references exceed the approved deduction control total.");
        var loanComponent = handoff.Lines.Where(l => l.LineType == "DEDUCTION:LOAN_DEDUCTION").Sum(l => l.Amount);
        if (loanRepayments + advanceRecoveries != loanComponent)
            throw new InvalidOperationException("Payroll loan deduction references do not reconcile to the immutable loan deduction component.");
        if (loanRepayments > 0m && string.IsNullOrWhiteSpace(config.LoanReceivableAccount))
            throw new InvalidOperationException("Configure the branch loan receivable account in Accounting before posting payroll loan repayments.");
        if (advanceRecoveries > 0m && string.IsNullOrWhiteSpace(config.AdvanceReceivableAccount))
            throw new InvalidOperationException("Configure the branch advance receivable account in Accounting before posting payroll advance recoveries.");
        var otherDeductions = deductions - loanRepayments - advanceRecoveries;
        var reset = context.SerialResetPolicy;
        var year = date.Year; var month = date.Month;
        var number = await vouchers.GenerateNextSerialNoAsync(handoff.BranchId.Value, year, month, config.VoucherType, reset, cancellationToken);
        var debitTotal = gross + employerSsc;
        var details = new List<GlVoucherDetail>();
        void Add(string account, decimal debit, decimal credit, int line)
        {
            if (debit == 0 && credit == 0) return;
            details.Add(new GlVoucherDetail { LineSer = line, AccountCode = account, Debit = debit, Credit = credit, LocalDebit = debit, LocalCredit = credit, BaseDebit = debit, BaseCredit = credit, CurrencyId = currency.Id, ExchangeRate = 1, BranchId = handoff.BranchId });
        }
        Add(config.SalaryExpense, gross, 0, 1);
        Add(config.EmployerSscExpense, employerSsc, 0, 2);
        Add(config.SalaryPayable, 0, net, 3);
        Add(config.SscPayable, 0, employeeSsc + employerSsc, 4);
        Add(config.TaxPayable, 0, tax, 5);
        Add(config.DeductionsPayable, 0, otherDeductions, 6);
        if (loanRepayments > 0m)
        {
            Add(config.LoanReceivableAccount!.Trim(), 0, loanRepayments, 7);
        }
        if (advanceRecoveries > 0m)
        {
            Add(config.AdvanceReceivableAccount!.Trim(), 0, advanceRecoveries, 8);
        }
        if (details.Sum(d => d.Debit) != details.Sum(d => d.Credit)) throw new InvalidOperationException("Generated journal voucher is not balanced.");
        var voucher = new GlVoucherHeader
        {
            BranchId = handoff.BranchId.Value, FiscalYearId = context.FiscalYearId, VoucherYear = year, VoucherMonth = month,
            VoucherType = config.VoucherType, VoucherNo = number, VoucherDate = date,
            Description = $"HR payroll handoff {handoff.HandoffReference}", TotalAmount = debitTotal,
            TotalLocalDebit = debitTotal, TotalLocalCredit = debitTotal, Status = 1,
            IsAutoRecord = true, SourceSystemCode = "HR_HANDOFF", SourceRefId = handoff.Id,
            CreationUser = user, CreationDate = DateTime.UtcNow, Details = details
        };
        await vouchers.AddVoucherAsync(voucher, cancellationToken);
        await vouchers.SaveChangesAsync(cancellationToken);
        db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
        {
            HandoffId = handoff.Id, VoucherId = voucher.Id, CreatedAtUtc = DateTime.UtcNow, CreatedBy = user
        });
        await db.SaveChangesAsync(cancellationToken);
        // A draft JV is not a posting receipt. Keep the handoff ACCEPTED until ERP confirms posting.
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return voucher.Id;
    }

    private static void EnsureActivePayrollJournalLink(HrAccountingHandoff handoff, long branchId, GlVoucherHeader voucher)
    {
        if (voucher.SourceSystemCode != "HR_HANDOFF" || voucher.SourceRefId != handoff.Id || voucher.BranchId != branchId ||
            voucher.IsReversed || voucher.Status is not (1 or 3) || voucher.Status == 3 && !voucher.PostDate.HasValue)
            throw new InvalidOperationException("The Accounting Journal Voucher linked to this handoff is missing, inactive, reversed, or mismatched.");
    }

    public async Task ConfirmJournalPostedAsync(long handoffId, long voucherId, string eventKey, string user, CancellationToken cancellationToken = default)
    {
        eventKey = eventKey?.Trim() ?? string.Empty;
        if (eventKey.Length is < 8 or > 160) throw new ArgumentException("A unique posting receipt event key is required.");
        if (db.Database.CurrentTransaction == null && unitOfWork != null)
        {
            var lockPlan = await ResolvePostingReceiptLockPeriodsAsync(handoffId, cancellationToken);
            if (lockPlan != null)
            {
                await unitOfWork.ExecuteForPayrollPeriodsAsync(lockPlan.CompanyId, lockPlan.RequiredPeriodCode, lockPlan.AffectedPeriodCodes, async () =>
                {
                    await ConfirmJournalPostedCoreAsync(handoffId, voucherId, eventKey, user, cancellationToken);
                    return true;
                }, cancellationToken);
                return;
            }
        }
        await ConfirmJournalPostedCoreAsync(handoffId, voucherId, eventKey, user, cancellationToken);
    }

    private async Task ConfirmJournalPostedCoreAsync(long handoffId, long voucherId, string eventKey, string user, CancellationToken cancellationToken)
    {
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).Include(h => h.Lines).SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff not found in active scope.");
        var voucher = await vouchers.GetByIdAsync(voucherId, cancellationToken)
            ?? throw new KeyNotFoundException("Journal voucher was not found.");
        if (voucher.SourceSystemCode != "HR_HANDOFF" || voucher.SourceRefId != handoff.Id || voucher.BranchId != handoff.BranchId)
            throw new InvalidOperationException("Journal voucher is not linked to this handoff.");
        var journalLink = await db.AccountingHandoffJournalLinks.SingleOrDefaultAsync(link => link.HandoffId == handoff.Id, cancellationToken);
        if (journalLink == null || journalLink.VoucherId != voucher.Id)
            throw new InvalidOperationException("Journal voucher does not match the Accounting link persisted for this handoff.");
        if (voucher.Status != 3 || !voucher.PostDate.HasValue)
            throw new InvalidOperationException("Only an ERP-posted journal voucher can produce a posting receipt.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{handoff.Id}|{voucher.Id}|{voucher.PostDate:O}|{voucher.TotalLocalDebit}|{voucher.TotalLocalCredit}")));
        var prior = await db.AccountingHandoffPostingReceipts.SingleOrDefaultAsync(r => r.HandoffId == handoff.Id || r.EventKey == eventKey, cancellationToken);
        if (prior != null)
        {
            if (prior.HandoffId == handoff.Id && prior.EventKey == eventKey && prior.PayloadHash == hash)
            {
                if (tx != null) await tx.RollbackAsync(cancellationToken);
                return;
            }
            throw new InvalidOperationException("A conflicting posting receipt already exists.");
        }
        if (handoff.DeliveryStatus != "ACCEPTED") throw new InvalidOperationException("Only an accepted handoff can receive its posting receipt.");
        string? settlementSnapshot = null;
        if (handoff.SourceType == "PAYROLL" && handoff.Operation == "SUBMIT")
            settlementSnapshot = await SettlePayrollReservationsAsync(handoff, user, cancellationToken);
        db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
        {
            HandoffId = handoff.Id, VoucherId = voucher.Id, EventKey = eventKey, PayloadHash = hash,
            ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = user, SettlementSnapshotJson = settlementSnapshot
        });
        handoff.DeliveryStatus = "POSTED";
        handoff.Transitions.Add(new HrAccountingHandoffTransition { FromStatus = "ACCEPTED", ToStatus = "POSTED", Actor = user, OccurredAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
    }

    private sealed record PostingReceiptLockPlan(long CompanyId, string RequiredPeriodCode, IReadOnlyCollection<string> AffectedPeriodCodes);

    private async Task<PostingReceiptLockPlan?> ResolvePostingReceiptLockPeriodsAsync(long handoffId, CancellationToken cancellationToken)
    {
        var handoff = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Lines)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff not found in active scope.");
        if (handoff.SourceType != "PAYROLL" || handoff.Operation != "SUBMIT") return null;
        var requiredPeriod = handoff.PayPeriod?.Trim() ?? string.Empty;
        if (requiredPeriod.Length is < 1 or > 20)
            throw new InvalidOperationException("Payroll posting receipt requires the source payroll period.");

        var scheduleIds = new List<long>();
        foreach (var line in handoff.Lines.Where(l => l.LineType == "REFERENCE:SCHEDULE"))
        {
            var parts = (line.SourceReference ?? string.Empty).Split(':');
            if (parts.Length != 4 || parts[0] != "SCHEDULE" || parts[2] != "PAYROLL_LINE" || !long.TryParse(parts[1], out var scheduleId))
                throw new InvalidOperationException("Payroll reservation reference is malformed.");
            scheduleIds.Add(scheduleId);
        }

        var affectedPeriods = new HashSet<string>(StringComparer.Ordinal) { requiredPeriod };
        if (scheduleIds.Count > 0)
        {
            var loanIds = await db.LoanRepaymentSchedules.AsNoTracking().Where(s => scheduleIds.Contains(s.Id))
                .Select(s => s.EmployeeLoanId).Distinct().ToArrayAsync(cancellationToken);
            if (loanIds.Length > 0)
            {
                var futureAndSourceLoanPeriods = await db.LoanRepaymentSchedules.AsNoTracking().Where(s => loanIds.Contains(s.EmployeeLoanId))
                    .Select(s => s.PayPeriod).Distinct().ToListAsync(cancellationToken);
                foreach (var period in futureAndSourceLoanPeriods)
                    if (!string.IsNullOrWhiteSpace(period)) affectedPeriods.Add(period.Trim());
            }
        }
        return new PostingReceiptLockPlan(db.HrScopeCompanyId, requiredPeriod, affectedPeriods.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public async Task<long> ReverseJournalVoucherAsync(long handoffId, DateTime reversalDate, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (reversalDate == default) throw new ArgumentException("An explicit reversal date is required.", nameof(reversalDate));
        reversalDate = reversalDate.Date;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1000) throw new ArgumentException("A journal reversal reason of 3..1000 characters is required.");

        if (db.Database.CurrentTransaction == null && unitOfWork != null)
        {
            var lockPlan = await ResolvePostingReceiptLockPeriodsAsync(handoffId, cancellationToken);
            if (lockPlan != null)
                return await unitOfWork.ExecuteForPayrollPeriodsAsync(
                    lockPlan.CompanyId,
                    lockPlan.RequiredPeriodCode,
                    lockPlan.AffectedPeriodCodes,
                    () => ReverseJournalVoucherCoreAsync(handoffId, reversalDate, reason, user, cancellationToken),
                    cancellationToken);
        }

        return await ReverseJournalVoucherCoreAsync(handoffId, reversalDate, reason, user, cancellationToken);
    }

    private async Task<long> ReverseJournalVoucherCoreAsync(long handoffId, DateTime reversalDate, string reason, string user, CancellationToken cancellationToken)
    {
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).Include(h => h.Lines)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff not found in active scope.");
        if (handoff.SourceType != "PAYROLL" || handoff.Operation != "SUBMIT")
            throw new InvalidOperationException("Only a posted payroll submission Journal Voucher can be reversed through this workflow.");

        var link = await db.AccountingHandoffJournalLinks.SingleOrDefaultAsync(x => x.HandoffId == handoff.Id, cancellationToken)
            ?? throw new InvalidOperationException("The payroll Journal Voucher link is missing.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Payroll handoff has no branch.");
        if (handoff.DeliveryStatus == "REVERSED")
        {
            var reversalTransition = handoff.Transitions.Where(t => t.ToStatus == "REVERSED").OrderByDescending(t => t.OccurredAtUtc).FirstOrDefault();
            if (reversalTransition?.Reason != reason) throw new InvalidOperationException("The payroll Journal Voucher reversal is immutable.");
            var originalList = await vouchers.GetBySourceAsync(branchId, "HR_HANDOFF", handoff.Id, cancellationToken);
            var reversalList = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", link.VoucherId, cancellationToken);
            if (originalList.Count != 1 || reversalList.Count != 1 || reversalList[0].VoucherDate.Date != reversalDate ||
                !originalList[0].IsReversed || !IsExactAccountingReversal(originalList[0], reversalList[0]))
                throw new InvalidOperationException("The stored Journal Voucher reversal cannot be verified.");
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            return reversalList[0].Id;
        }
        if (handoff.DeliveryStatus != "POSTED") throw new InvalidOperationException("Only a posted handoff can have its Journal Voucher reversed.");

        var payments = await db.AccountingHandoffPayments.Where(p => p.HandoffId == handoff.Id).ToListAsync(cancellationToken);
        if (payments.Any(p => p.Status is not ("FAILED" or "REVERSED")))
            throw new InvalidOperationException("Void prepared payments and reverse every confirmed payment before reversing the payroll Journal Voucher.");
        foreach (var payment in payments)
        {
            var linkedPaymentVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
            if (linkedPaymentVouchers.Count > 1)
                throw new InvalidOperationException($"Payment {payment.Id} has multiple linked Payment Vouchers; reconcile Accounting before reversing the payroll Journal Voucher.");
            if (payment.Status == "FAILED")
            {
                if (string.IsNullOrWhiteSpace(payment.FailureReason) || !payment.FailedAtUtc.HasValue || string.IsNullOrWhiteSpace(payment.FailedBy))
                    throw new InvalidOperationException($"Failed payment {payment.Id} has incomplete audit data.");
                if (linkedPaymentVouchers.Count == 1 && linkedPaymentVouchers[0].Status != 4)
                    throw new InvalidOperationException($"Failed payment {payment.Id} still has a non-voided Payment Voucher.");
                continue;
            }

            if (linkedPaymentVouchers.Count != 1 || !payment.ReversalVoucherId.HasValue ||
                string.IsNullOrWhiteSpace(payment.ReversalReason) || string.IsNullOrWhiteSpace(payment.ReversalExternalReference) ||
                string.IsNullOrWhiteSpace(payment.ReversalEvidenceReference) || !payment.ReversedAtUtc.HasValue || string.IsNullOrWhiteSpace(payment.ReversedBy))
                throw new InvalidOperationException($"Reversed payment {payment.Id} is missing its original/reversal voucher or reversal audit data.");
            var originalPaymentVoucher = linkedPaymentVouchers[0];
            var reversalPaymentVoucher = await vouchers.GetByIdAsync(payment.ReversalVoucherId.Value, cancellationToken);
            var linkedReversals = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", originalPaymentVoucher.Id, cancellationToken);
            if (originalPaymentVoucher.SourceSystemCode != "HR_PAYMENT" || originalPaymentVoucher.SourceRefId != payment.Id ||
                !originalPaymentVoucher.IsReversed || reversalPaymentVoucher == null || linkedReversals.Count != 1 ||
                linkedReversals[0].Id != reversalPaymentVoucher.Id || !IsExactAccountingReversal(originalPaymentVoucher, reversalPaymentVoucher))
                throw new InvalidOperationException($"Reversed payment {payment.Id} does not have a verifiable exact Accounting reversal.");
        }
        var postingReceipt = await db.AccountingHandoffPostingReceipts.SingleOrDefaultAsync(r => r.HandoffId == handoff.Id, cancellationToken)
            ?? throw new InvalidOperationException("The payroll Journal Voucher has no posting receipt.");
        if (postingReceipt.VoucherId != link.VoucherId) throw new InvalidOperationException("Posting receipt and Journal Voucher link do not match.");
        var journal = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
        if (journal.Id != postingReceipt.VoucherId) throw new InvalidOperationException("Posting receipt does not reference the active payroll Journal Voucher.");
        if (reversalDate < journal.VoucherDate.Date) throw new ArgumentException("The reversal date cannot precede the original journal date.", nameof(reversalDate));

        var settlementReferences = handoff.Lines.Where(l => l.LineType is "REFERENCE:ADVANCE" or "REFERENCE:SCHEDULE").ToArray();
        List<PayrollSettlementSnapshot> settlementSnapshots;
        if (string.IsNullOrWhiteSpace(postingReceipt.SettlementSnapshotJson))
        {
            if (settlementReferences.Length != 0)
                throw new InvalidOperationException("This posted payroll predates settlement snapshots; automatic reversal is blocked to avoid guessing loan or advance balances.");
            settlementSnapshots = [];
        }
        else
        {
            settlementSnapshots = JsonSerializer.Deserialize<List<PayrollSettlementSnapshot>>(postingReceipt.SettlementSnapshotJson)
                ?? throw new InvalidOperationException("Payroll settlement snapshot is malformed; review accounting history before reversal.");
            var unmatchedReferences = settlementReferences.ToList();
            var snapshotReferencesMatch = settlementSnapshots.All(snapshot =>
            {
                var matchIndex = unmatchedReferences.FindIndex(line => MatchesSettlementSnapshot(line, snapshot));
                if (matchIndex < 0) return false;
                unmatchedReferences.RemoveAt(matchIndex);
                return true;
            });
            if (settlementSnapshots.Count != settlementReferences.Length || !snapshotReferencesMatch || unmatchedReferences.Count != 0)
                throw new InvalidOperationException("Payroll settlement snapshot does not match the handoff references.");
        }

        var reversalContext = await payrollPosting.ResolveReversalAsync(branchId, reversalDate, journal.VoucherType, cancellationToken);
        await glVouchers.ReverseVoucherAsync(journal.Id, user, reason, reversalDate, reversalContext.FiscalYearId,
            reversalContext.SerialResetPolicy, cancellationToken);
        var reversalVouchers = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", journal.Id, cancellationToken);
        if (!journal.IsReversed || reversalVouchers.Count != 1 || !IsExactAccountingReversal(journal, reversalVouchers[0]))
            throw new InvalidOperationException("The ERP Journal Voucher reversal was not created or does not exactly offset its source.");

        await RestorePayrollSettlementSnapshotAsync(settlementSnapshots, user, cancellationToken);
        handoff.DeliveryStatus = "REVERSED";
        handoff.Transitions.Add(new HrAccountingHandoffTransition
        {
            FromStatus = "POSTED", ToStatus = "REVERSED", Actor = user, Reason = reason, OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return reversalVouchers[0].Id;
    }

    public async Task<AccountingHandoffPayment> PreparePaymentAsync(long handoffId, decimal amount, string currency, string method, string idempotencyKey, string user, CancellationToken cancellationToken = default)
    {
        currency = Normalize(currency); method = Normalize(method); idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (amount <= 0 || amount > 99999999999999.9999m) throw new ArgumentOutOfRangeException(nameof(amount));
        if (currency.Length is < 1 or > 20 || method.Length is < 1 or > 50 || idempotencyKey.Length is < 8 or > 160)
            throw new ArgumentException("Payment currency, method, and idempotency key are invalid.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff not found in active scope.");
        if (handoff.DeliveryStatus != "POSTED") throw new InvalidOperationException("Payment can be prepared only after the journal posting receipt.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        if (ResolveHandoffCurrency(handoff.Lines) != currency)
            throw new InvalidOperationException("Payment currency does not match the approved handoff currency.");
        var paymentMethod = await ResolveVoucherPaymentMethodAsync(branchId, method, cancellationToken);
        var methodCategory = PaymentCategory(paymentMethod.MethodType);
        var paymentMethodCode = Normalize(paymentMethod.Code);
        var prior = await db.AccountingHandoffPayments.SingleOrDefaultAsync(p => p.HandoffId == handoffId && p.IdempotencyKey == idempotencyKey, cancellationToken);
        if (prior != null)
        {
            if (prior.Amount != amount || Normalize(prior.CurrencyCode) != currency || prior.Method != methodCategory ||
                (prior.PaymentMethodCode != null && Normalize(prior.PaymentMethodCode) != paymentMethodCode))
                throw new InvalidOperationException("Idempotency key was reused for a different payment instruction.");
            if (prior.Status != "PREPARED")
            {
                var priorVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", prior.Id, cancellationToken);
                if (priorVouchers.Count > 1) throw new InvalidOperationException("More than one Payment Voucher is linked to this payment instruction.");
                if (priorVouchers.Count == 1)
                {
                    await ValidatePaymentReplayVoucherAsync(prior, handoff, paymentMethod, priorVouchers[0], cancellationToken);
                }
                else if (prior.PaymentMethodCode == null || prior.Status != "FAILED")
                    throw new InvalidOperationException("The payment instruction has no saved Payment Method Code or unique Payment Voucher to verify this idempotent replay.");
                prior.PaymentVoucherId = priorVouchers.SingleOrDefault()?.Id;
                await tx.RollbackAsync(cancellationToken);
                return prior;
            }
            prior.PaymentMethodCode ??= paymentMethodCode;
            prior.PaymentVoucherId = await EnsurePaymentVoucherAsync(prior, handoff, paymentMethod, user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return prior;
        }
        var payable = handoff.Lines.Where(l => l.LineType == "CONTROL:NET_PAY").Sum(l => l.Amount);
        var reserved = await db.AccountingHandoffPayments.Where(p => p.HandoffId == handoffId && new[] { "PREPARED", "CONFIRMED" }.Contains(p.Status)).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        if (reserved + amount > payable) throw new InvalidOperationException("Payment amount exceeds the remaining payable amount.");
        var sequence = (await db.AccountingHandoffPayments.Where(p => p.HandoffId == handoffId).MaxAsync(p => (int?)p.PaymentSequence, cancellationToken) ?? 0) + 1;
        var payment = new AccountingHandoffPayment { HandoffId = handoffId, PaymentSequence = sequence, IdempotencyKey = idempotencyKey, CurrencyCode = currency, Amount = amount, Method = methodCategory, PaymentMethodCode = paymentMethodCode, Status = "PREPARED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = user };
        db.AccountingHandoffPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        payment.PaymentVoucherId = await EnsurePaymentVoucherAsync(payment, handoff, paymentMethod, user, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return payment;
    }

    public async Task<AccountingHandoffPayment> ConfirmPaymentAsync(long paymentId, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default)
    {
        externalReference = externalReference?.Trim() ?? string.Empty; evidenceReference = evidenceReference?.Trim() ?? string.Empty;
        if (externalReference.Length is < 1 or > 200 || evidenceReference.Length is < 1 or > 500) throw new ArgumentException("A payment reference and evidence reference are required.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await db.AccountingHandoffPayments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment instruction not found in active scope.");
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleOrDefaultAsync(h => h.Id == payment.HandoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment handoff not found in active scope.");
        if (handoff.DeliveryStatus != "POSTED") throw new InvalidOperationException("Payment handoff must remain posted before payment confirmation.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        var handoffCurrency = ResolveHandoffCurrency(handoff.Lines);
        var linkedVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
        if (payment.Status == "CONFIRMED" && linkedVouchers.Count == 0)
        {
            if (payment.ExternalReference == externalReference && payment.EvidenceReference == evidenceReference)
            { await tx.RollbackAsync(cancellationToken); return payment; }
            throw new InvalidOperationException("Confirmed payment cannot be changed; use the reversal workflow.");
        }
        if (linkedVouchers.Count != 1) throw new InvalidOperationException("A unique HR Payment Voucher must be linked before confirming payment.");
        var linkedVoucher = linkedVouchers[0];
        var journal = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
        var voucherCurrencyIds = linkedVoucher.Details.Select(d => d.CurrencyId).Distinct().ToArray();
        var voucherCurrency = voucherCurrencyIds.Length == 1
            ? await db.SysCurrencies.SingleOrDefaultAsync(c => c.Id == voucherCurrencyIds[0] && c.ShortNameEn != null, cancellationToken)
            : null;
        var debits = linkedVoucher.Details.Where(d => d.Debit > 0).ToArray();
        var credits = linkedVoucher.Details.Where(d => d.Credit > 0).ToArray();
        var creditedAccountCode = credits.Length == 1 ? credits[0].AccountCode : null;
        PaymentMethod? paymentMethod = null;
        if (payment.PaymentMethodCode != null)
            paymentMethod = await db.PaymentMethods.SingleOrDefaultAsync(m => m.BranchId == branchId && m.Code.Trim().ToUpper() == payment.PaymentMethodCode, cancellationToken);
        else if (creditedAccountCode != null)
        {
            var legacyMethods = await db.PaymentMethods.Where(m => m.BranchId == branchId && m.GlAccountCode == creditedAccountCode &&
                (payment.Method == "OTHER" ? m.MethodType.Trim().ToUpper() != "BANK" && m.MethodType.Trim().ToUpper() != "CASH" : m.MethodType.Trim().ToUpper() == payment.Method))
                .OrderBy(m => m.Id).Take(2).ToListAsync(cancellationToken);
            if (legacyMethods.Count == 1) paymentMethod = legacyMethods[0];
        }
        if (paymentMethod == null) throw new InvalidOperationException("The configured payment method linked to this payment instruction is missing or ambiguous.");
        if (linkedVoucher.VoucherType != 103 || linkedVoucher.Status != 3 || linkedVoucher.IsReversed || !linkedVoucher.PostDate.HasValue || linkedVoucher.BranchId != branchId ||
            linkedVoucher.TotalAmount != payment.Amount || linkedVoucher.TotalLocalDebit != payment.Amount || linkedVoucher.TotalLocalCredit != payment.Amount ||
            linkedVoucher.Details.Count != 2 || debits.Length != 1 || credits.Length != 1 || PaymentCategory(paymentMethod.MethodType) != payment.Method ||
            debits[0].Debit != payment.Amount || debits[0].AccountCode != journal.Details.Single(d => d.LineSer == 3).AccountCode ||
            credits[0].Credit != payment.Amount || credits[0].AccountCode != paymentMethod.GlAccountCode ||
            Normalize(voucherCurrency?.ShortNameEn) != Normalize(payment.CurrencyCode) ||
            Normalize(payment.CurrencyCode) != handoffCurrency)
            throw new InvalidOperationException("Payment confirmation requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount.");
        payment.PaymentVoucherId = linkedVoucher.Id;
        if (payment.Status == "CONFIRMED")
        {
            if (payment.ExternalReference == externalReference && payment.EvidenceReference == evidenceReference) { await tx.RollbackAsync(cancellationToken); return payment; }
            throw new InvalidOperationException("Confirmed payment cannot be changed; use the reversal workflow.");
        }
        if (payment.Status != "PREPARED") throw new InvalidOperationException("Only a prepared payment can be confirmed.");
        payment.Status = "CONFIRMED"; payment.ExternalReference = externalReference; payment.EvidenceReference = evidenceReference;
        payment.ConfirmedAtUtc = DateTime.UtcNow; payment.ConfirmedBy = user;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return payment;
    }

    public async Task<AccountingHandoffPayment> FailPaymentAsync(long paymentId, string reason, string user, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1000) throw new ArgumentException("A failure or cancellation reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var payment = await db.AccountingHandoffPayments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment instruction not found in active scope.");
        if (payment.Status == "FAILED")
        {
            if (payment.FailureReason == reason) { await tx.RollbackAsync(cancellationToken); return payment; }
            throw new InvalidOperationException("A failed payment instruction is immutable; create a new payment instruction with a new idempotency key.");
        }
        if (payment.Status != "PREPARED") throw new InvalidOperationException("Only a prepared payment can be failed or cancelled; posted payments require the reversal workflow.");

        var handoff = await db.HrAccountingHandoffs.SingleOrDefaultAsync(h => h.Id == payment.HandoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment handoff not found in active scope.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        _ = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
        var linkedVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
        if (linkedVouchers.Count > 1) throw new InvalidOperationException("More than one Payment Voucher is linked to this payment instruction.");
        if (linkedVouchers.Count == 1)
        {
            var voucher = linkedVouchers[0];
            if (voucher.Status == 3) throw new InvalidOperationException("A posted Payment Voucher cannot be marked failed; use the Accounting reversal workflow.");
            if (voucher.Status != 4 && !await paymentVouchers.DeletePaymentVoucherAsync(voucher.Id, user, cancellationToken))
                throw new InvalidOperationException("The draft Payment Voucher could not be voided.");
            payment.PaymentVoucherId = voucher.Id;
        }

        payment.Status = "FAILED";
        payment.FailureReason = reason;
        payment.FailedAtUtc = DateTime.UtcNow;
        payment.FailedBy = user;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return payment;
    }

    public async Task<AccountingHandoffPayment> ReversePaymentAsync(long paymentId, string reason, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        externalReference = externalReference?.Trim() ?? string.Empty;
        evidenceReference = evidenceReference?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1000 || externalReference.Length is < 1 or > 200 || evidenceReference.Length is < 1 or > 500)
            throw new ArgumentException("A reversal reason, external reversal reference, and evidence reference are required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var payment = await db.AccountingHandoffPayments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment instruction not found in active scope.");
        if (payment.Status == "REVERSED")
        {
            if (payment.ReversalReason == reason && payment.ReversalExternalReference == externalReference && payment.ReversalEvidenceReference == evidenceReference)
            { payment.PaymentVoucherId = await FindPaymentVoucherIdAsync(payment, cancellationToken); await tx.RollbackAsync(cancellationToken); return payment; }
            throw new InvalidOperationException("A reversed payment is immutable.");
        }
        if (payment.Status != "CONFIRMED") throw new InvalidOperationException("Only a confirmed payment with external payment evidence can be reversed.");

        var handoff = await db.HrAccountingHandoffs.SingleOrDefaultAsync(h => h.Id == payment.HandoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment handoff not found in active scope.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        _ = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
        var linkedVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
        if (linkedVouchers.Count != 1) throw new InvalidOperationException("A unique HR Payment Voucher must be linked before reversal.");
        var original = linkedVouchers[0];
        IReadOnlyList<GlVoucherHeader> reversals = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", original.Id, cancellationToken);
        if (!original.IsReversed)
        {
            if (original.Status != 3 || !original.PostDate.HasValue) throw new InvalidOperationException("Only a posted Payment Voucher can be reversed.");
            await glVouchers.ReverseVoucherAsync(original.Id, user, reason, cancellationToken);
            reversals = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", original.Id, cancellationToken);
        }
        if (!original.IsReversed || reversals.Count != 1 || !IsExactAccountingReversal(original, reversals[0]))
            throw new InvalidOperationException("The ERP reversal voucher was not created or verified.");

        payment.Status = "REVERSED";
        payment.ReversalReason = reason;
        payment.ReversalExternalReference = externalReference;
        payment.ReversalEvidenceReference = evidenceReference;
        payment.ReversedAtUtc = DateTime.UtcNow;
        payment.ReversedBy = user;
        payment.ReversalVoucherId = reversals[0].Id;
        payment.PaymentVoucherId = original.Id;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return payment;
    }

    private async Task<long?> FindPaymentVoucherIdAsync(AccountingHandoffPayment payment, CancellationToken cancellationToken)
    {
        var branchId = await db.HrAccountingHandoffs.Where(h => h.Id == payment.HandoffId).Select(h => h.BranchId).SingleOrDefaultAsync(cancellationToken);
        if (!branchId.HasValue) return null;
        var linked = await vouchers.GetBySourceAsync(branchId.Value, "HR_PAYMENT", payment.Id, cancellationToken);
        if (linked.Count > 1) throw new InvalidOperationException("More than one Payment Voucher is linked to this payment.");
        return linked.SingleOrDefault()?.Id;
    }

    private static bool IsExactAccountingReversal(GlVoucherHeader original, GlVoucherHeader reversal) =>
        reversal.Status == 3 && !reversal.IsReversed && reversal.PostDate.HasValue &&
        reversal.BranchId == original.BranchId && reversal.VoucherType == original.VoucherType &&
        reversal.SourceSystemCode == "GL_REVERSAL" && reversal.SourceRefId == original.Id &&
        reversal.Details.Count == original.Details.Count &&
        reversal.TotalAmount == original.TotalAmount &&
        reversal.TotalLocalDebit == original.TotalLocalCredit && reversal.TotalLocalCredit == original.TotalLocalDebit &&
        original.Details.All(d => reversal.Details.Count(r => IsInverseAccountingLine(d, r)) == 1);

    private static bool IsInverseAccountingLine(GlVoucherDetail original, GlVoucherDetail reversal) =>
        original.AccountCode == reversal.AccountCode && original.Debit == reversal.Credit && original.Credit == reversal.Debit &&
        original.LocalDebit == reversal.LocalCredit && original.LocalCredit == reversal.LocalDebit &&
        original.BaseDebit == reversal.BaseCredit && original.BaseCredit == reversal.BaseDebit &&
        original.CurrencyId == reversal.CurrencyId && original.ExchangeRate == reversal.ExchangeRate &&
        original.CostCenterCode == reversal.CostCenterCode &&
        original.CostCenterMgrCode == reversal.CostCenterMgrCode && original.CostCenterMnrCode == reversal.CostCenterMnrCode &&
        original.PartyType == reversal.PartyType && original.PartyCode == reversal.PartyCode;

    private static string PaymentCategory(string methodType) => Normalize(methodType) switch
    {
        "BANK" => "BANK",
        "CASH" => "CASH",
        _ => "OTHER"
    };

    private async Task<PaymentMethod> ResolveVoucherPaymentMethodAsync(long branchId, string method, CancellationToken cancellationToken)
    {
        var configured = await db.PaymentMethods.SingleOrDefaultAsync(m => m.BranchId == branchId && m.IsActive && m.ShowInVouchers &&
            m.Code.Trim().ToUpper() == method, cancellationToken);
        if (configured != null) return configured;

        if (method is not ("BANK" or "CASH" or "OTHER"))
            throw new InvalidOperationException("Select an active Accounting payment method configured for this branch and enabled for vouchers.");

        var candidates = await db.PaymentMethods.Where(m => m.BranchId == branchId && m.IsActive && m.ShowInVouchers &&
            (method == "BANK" ? m.MethodType.Trim().ToUpper() == "BANK" : method == "CASH" ? m.MethodType.Trim().ToUpper() == "CASH" :
                m.MethodType.Trim().ToUpper() != "BANK" && m.MethodType.Trim().ToUpper() != "CASH")).OrderBy(m => m.Id).Take(2).ToListAsync(cancellationToken);
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException("No active Accounting payment method matches the requested method for this branch."),
            _ => throw new InvalidOperationException("More than one Accounting payment method matches; provide its configured method code.")
        };
    }

    private async Task ValidatePaymentReplayVoucherAsync(AccountingHandoffPayment payment, HrAccountingHandoff handoff,
        PaymentMethod requestedMethod, GlVoucherHeader voucher, CancellationToken cancellationToken)
    {
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        var currencyCode = Normalize(payment.CurrencyCode);
        var currencies = await db.SysCurrencies.Where(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == currencyCode)
            .Take(2).ToListAsync(cancellationToken);
        var journalLink = await db.AccountingHandoffJournalLinks.SingleOrDefaultAsync(x => x.HandoffId == handoff.Id, cancellationToken);
        var journal = journalLink == null ? null : await vouchers.GetByIdAsync(journalLink.VoucherId, cancellationToken);
        var payableAccount = journal?.Details.SingleOrDefault(d => d.LineSer == 3 && d.Credit > 0)?.AccountCode;
        var debitLines = voucher.Details.Where(d => d.Debit > 0).ToArray();
        var creditLines = voucher.Details.Where(d => d.Credit > 0).ToArray();
        var validLifecycle = payment.Status switch
        {
            "CONFIRMED" => voucher.Status == 3 && !voucher.IsReversed && voucher.PostDate.HasValue,
            "FAILED" => voucher.Status == 4 && !voucher.IsReversed,
            "REVERSED" => voucher.Status == 3 && voucher.IsReversed && voucher.PostDate.HasValue,
            _ => false
        };
        if (!validLifecycle || currencies.Count != 1 || payableAccount == null ||
            voucher.SourceSystemCode != "HR_PAYMENT" || voucher.SourceRefId != payment.Id || voucher.VoucherType != 103 ||
            voucher.BranchId != branchId || voucher.TotalAmount != payment.Amount ||
            voucher.TotalLocalDebit != payment.Amount || voucher.TotalLocalCredit != payment.Amount ||
            voucher.Details.Count != 2 || debitLines.Length != 1 || creditLines.Length != 1 ||
            debitLines[0].Debit != payment.Amount || debitLines[0].AccountCode != payableAccount ||
            creditLines[0].Credit != payment.Amount || creditLines[0].AccountCode != requestedMethod.GlAccountCode ||
            debitLines[0].CurrencyId != currencies[0].Id || creditLines[0].CurrencyId != currencies[0].Id ||
            debitLines[0].BranchId != branchId || creditLines[0].BranchId != branchId ||
            PaymentCategory(requestedMethod.MethodType) != payment.Method)
            throw new InvalidOperationException("The legacy Payment Voucher does not prove that this idempotent replay uses the original method, branch, currency, and amount.");
    }

    private async Task<long> EnsurePaymentVoucherAsync(AccountingHandoffPayment payment, HrAccountingHandoff handoff,
        PaymentMethod paymentMethod, string user, CancellationToken cancellationToken)
    {
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Accounting handoff has no branch.");
        var existing = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
        if (existing.Count > 1) throw new InvalidOperationException("More than one Payment Voucher is linked to this payment instruction.");
        if (existing.Count == 1)
        {
            var existingVoucher = existing[0];
            var existingPaymentCurrency = Normalize(payment.CurrencyCode);
            var existingCurrency = await db.SysCurrencies.SingleOrDefaultAsync(c => c.ShortNameEn != null &&
                c.ShortNameEn.Trim().ToUpper() == existingPaymentCurrency, cancellationToken)
                ?? throw new InvalidOperationException("Payment currency no longer resolves to one ERP currency.");
            var existingJournal = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
            var debitLines = existingVoucher.Details.Where(d => d.Debit > 0).ToArray();
            var creditLines = existingVoucher.Details.Where(d => d.Credit > 0).ToArray();
            if (existingVoucher.SourceSystemCode != "HR_PAYMENT" || existingVoucher.SourceRefId != payment.Id ||
                existingVoucher.VoucherType != 103 || existingVoucher.BranchId != branchId || existingVoucher.IsReversed ||
                existingVoucher.Status is not (1 or 3) || existingVoucher.Status == 3 && !existingVoucher.PostDate.HasValue ||
                existingVoucher.TotalAmount != payment.Amount || existingVoucher.TotalLocalDebit != payment.Amount ||
                existingVoucher.TotalLocalCredit != payment.Amount || existingVoucher.Details.Count != 2 ||
                debitLines.Length != 1 || creditLines.Length != 1 || debitLines[0].Debit != payment.Amount ||
                debitLines[0].AccountCode != existingJournal.Details.Single(d => d.LineSer == 3).AccountCode ||
                creditLines[0].Credit != payment.Amount || creditLines[0].AccountCode != paymentMethod.GlAccountCode ||
                debitLines[0].CurrencyId != existingCurrency.Id || creditLines[0].CurrencyId != existingCurrency.Id ||
                debitLines[0].BranchId != branchId || creditLines[0].BranchId != branchId ||
                PaymentCategory(paymentMethod.MethodType) != payment.Method)
                throw new InvalidOperationException("The existing Payment Voucher does not match this payment instruction's method, branch, currency, amount, or payroll payable account.");

            return existingVoucher.Id;
        }

        var date = DateTime.UtcNow.Date;
        var context = await payrollPosting.ResolveAsync(branchId, date, cancellationToken);
        var journal = await GetPostedPayrollJournalAsync(handoff.Id, cancellationToken);
        var paymentCurrency = Normalize(payment.CurrencyCode);
        var currency = await db.SysCurrencies.SingleOrDefaultAsync(c => c.Id == context.Configuration.CurrencyId && c.ShortNameEn != null &&
            c.ShortNameEn.Trim().ToUpper() == paymentCurrency, cancellationToken)
            ?? throw new InvalidOperationException("Payment currency does not match the configured Accounting base currency.");
        if (ResolveHandoffCurrency(handoff.Lines) != paymentCurrency)
            throw new InvalidOperationException("Payment currency does not match the approved handoff currency.");
        if (paymentMethod.BankAccountId.HasValue && !await db.BankAccounts.AnyAsync(a => a.Id == paymentMethod.BankAccountId &&
                a.BranchId == branchId && a.IsActive && a.CurrencyId == currency.Id && a.GlAccountCode == paymentMethod.GlAccountCode, cancellationToken))
            throw new InvalidOperationException("The payment method's bank account is inactive, mismatched to this branch/currency, or uses another GL account.");
        if (paymentMethod.CashRegisterId.HasValue && !await db.CashRegisters.AnyAsync(r => r.Id == paymentMethod.CashRegisterId &&
                r.BranchId == branchId && r.IsActive && r.CurrencyId == currency.Id && r.GlAccountCode == paymentMethod.GlAccountCode, cancellationToken))
            throw new InvalidOperationException("The payment method's cash register is inactive, mismatched to this branch/currency, or uses another GL account.");

        var voucher = await paymentVouchers.CreatePaymentVoucherAsync(new CreatePaymentVoucherDto
        {
            BranchId = branchId,
            FiscalYearId = context.FiscalYearId,
            VoucherDate = date,
            Description = $"HR payroll payment handoff {handoff.HandoffReference} / {payment.PaymentSequence}",
            PaymentMethod = paymentMethod.MethodType,
            CashOrBankAccountCode = paymentMethod.GlAccountCode,
            CurrencyId = currency.Id,
            ExchangeRate = 1m,
            TotalAmount = payment.Amount,
            ExpenseAccountCode = journal.Details.Single(d => d.LineSer == 3).AccountCode,
            BeneficiaryName = $"Payroll handoff {handoff.HandoffReference}",
            TransferReference = payment.IdempotencyKey,
            AutoPost = false
        }, user, cancellationToken);

        var header = await db.GlVoucherHeaders.SingleAsync(h => h.Id == voucher.VoucherId, cancellationToken);
        header.SourceSystemCode = "HR_PAYMENT";
        header.SourceRefId = payment.Id;
        header.IsAutoRecord = true;
        await db.SaveChangesAsync(cancellationToken);
        return header.Id;
    }

    private async Task<GlVoucherHeader> GetPostedPayrollJournalAsync(long handoffId, CancellationToken cancellationToken)
    {
        var link = await db.AccountingHandoffJournalLinks.SingleOrDefaultAsync(x => x.HandoffId == handoffId, cancellationToken)
            ?? throw new InvalidOperationException("A linked payroll Journal Voucher is required before creating a Payment Voucher.");
        var journal = await vouchers.GetByIdAsync(link.VoucherId, cancellationToken)
            ?? throw new InvalidOperationException("The linked payroll Journal Voucher is missing.");
        if (journal.Status != 3 || journal.IsReversed || !journal.PostDate.HasValue || journal.SourceSystemCode != "HR_HANDOFF" || journal.SourceRefId != handoffId)
            throw new InvalidOperationException("The linked payroll Journal Voucher must remain posted before creating or confirming payment.");
        if (!journal.Details.Any(d => d.LineSer == 3 && d.Credit > 0))
            throw new InvalidOperationException("The posted payroll Journal Voucher is missing its salary payable line.");
        return journal;
    }

    public async Task<HrAccountingHandoff> CreateApprovedFundingHandoffAsync(string sourceType, long sourceId, string user, CancellationToken cancellationToken = default)
    {
        sourceType = Normalize(sourceType);
        if (sourceType is not ("LOAN" or "ADVANCE" or "EOS")) throw new ArgumentException("Funding source must be LOAN, ADVANCE, or EOS.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        string employeeCode; long? branchId; decimal amount; string? currency; string status; DateTime? approvedAt;
        if (sourceType == "LOAN")
        {
            var loan = await db.EmployeeLoans.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Loan request not found in active scope.");
            if (loan.Status != "APPROVED") throw new InvalidOperationException("Only an HR-approved loan can be sent for funding.");
            employeeCode = loan.EmployeeCode; branchId = loan.Employee?.BranchId; amount = loan.PrincipalAmount; currency = loan.CurrencyCode; status = loan.Status; approvedAt = loan.ApprovalDate;
        }
        else if (sourceType == "ADVANCE")
        {
            var advance = await db.EmployeeAdvances.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Advance request not found in active scope.");
            if (advance.Status != "APPROVED") throw new InvalidOperationException("Only an HR-approved advance can be sent for funding.");
            employeeCode = advance.EmployeeCode; branchId = advance.Employee?.BranchId; amount = advance.AdvanceAmount; currency = advance.CurrencyCode; status = advance.Status; approvedAt = advance.ApprovalDate;
        }
        else
        {
            await LockEndOfServiceSettlementAsync(sourceId, tx!, cancellationToken);
            var settlement = await db.EndOfServiceSettlements.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("End of service settlement not found in active scope.");
            if (settlement.Status != "APPROVED") throw new InvalidOperationException("Only an HR-approved end of service settlement can be sent for payout.");
            if (settlement.NetSettlementAmount <= 0) throw new InvalidOperationException("End of service settlement net amount must be greater than zero for payout handoff.");
            employeeCode = settlement.EmployeeCode; branchId = settlement.BranchId ?? settlement.Employee?.BranchId; amount = settlement.NetSettlementAmount; currency = settlement.CurrencyCode; status = settlement.Status; approvedAt = settlement.ApprovedAtUtc;
        }
        currency = Normalize(currency);
        if (currency.Length == 0) throw new InvalidOperationException("An explicit currency is required; legacy loan/advance currency is never inferred.");
        if (!branchId.HasValue) throw new InvalidOperationException("A branch is required for funding requests.");
        var branch = await db.SysBranches.SingleOrDefaultAsync(x => x.Id == branchId.Value && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Funding branch is missing or inactive.");
        var companyId = branch.CompanyId ?? throw new InvalidOperationException("Funding branch has no company.");
        if (db.HrScopeEnabled && companyId != db.HrScopeCompanyId) throw new UnauthorizedAccessException("Funding request is outside company scope.");
        if (await db.SysCurrencies.CountAsync(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == currency, cancellationToken) != 1)
            throw new InvalidOperationException("Funding currency must match one unique ERP currency code.");
        var sourceKey = sourceId.ToString();
        var priorHandoffs = await db.HrAccountingHandoffs.Include(h => h.Lines)
            .Where(h => h.SourceType == sourceType && h.SourceId == sourceKey && h.Operation == "FUND")
            .OrderByDescending(h => h.SourceVersion).ToListAsync(cancellationToken);
        var latest = priorHandoffs.FirstOrDefault();
        if (latest != null && latest.DeliveryStatus != "REVERSED")
        {
            await tx.RollbackAsync(cancellationToken);
            return latest;
        }
        var sourceVersion = latest == null ? 1 : latest.SourceVersion + 1;
        if (latest != null)
        {
            var priorReceipt = await db.AccountingLoanFundingReceipts.SingleOrDefaultAsync(r => r.HandoffId == latest.Id, cancellationToken);
            if (priorReceipt?.ReversalVoucherId == null || priorReceipt.ReversalEventKey == null || priorReceipt.ReversedAtUtc == null)
                throw new InvalidOperationException("A new funding version cannot be created until the prior funding reversal receipt is complete.");
        }
        var payload = JsonSerializer.Serialize(new { sourceType, sourceId, sourceVersion, amount, currency, employeeCode, branchId, approvedAt });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var handoff = new HrAccountingHandoff
        {
            CompanyId = companyId, BranchId = branchId, SourceType = sourceType, SourceId = sourceKey,
            SourceVersion = sourceVersion, Operation = "FUND", SourceApprovedAtUtc = approvedAt, HandoffReference = $"HR-{sourceType}-{companyId}-{sourceId}-{sourceVersion}",
            PayloadHash = hash, DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = user,
            Lines = [new HrAccountingHandoffLine { LineType = sourceType == "EOS" ? "EOS_SETTLEMENT" : "PRINCIPAL", CurrencyCode = currency, Amount = amount, BranchId = branchId, SourceReference = $"{sourceType}:{sourceId}" }],
            Transitions = [new HrAccountingHandoffTransition { FromStatus = status, ToStatus = "QUEUED", Actor = user, OccurredAtUtc = DateTime.UtcNow }],
            OutboxMessages = [new HrAccountingOutboxMessage { EventKey = $"{companyId}:{sourceType}:{sourceId}:{sourceVersion}:FUND", EventType = sourceType == "EOS" ? "EOS_SETTLEMENT_PAYOUT_REQUEST" : "LOAN_FUNDING_REQUEST", PayloadHash = hash, Status = "PENDING", CreatedAtUtc = DateTime.UtcNow }]
        };
        db.HrAccountingHandoffs.Add(handoff);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is OracleException { Number: 1 })
        {
            await tx.RollbackAsync(cancellationToken);
            db.Entry(handoff).State = EntityState.Detached;
            foreach (var line in handoff.Lines) db.Entry(line).State = EntityState.Detached;
            foreach (var transition in handoff.Transitions) db.Entry(transition).State = EntityState.Detached;
            foreach (var outboxMessage in handoff.OutboxMessages) db.Entry(outboxMessage).State = EntityState.Detached;

            var concurrent = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Lines)
                .SingleOrDefaultAsync(h => h.SourceType == sourceType &&
                    h.SourceId == sourceKey && h.SourceVersion == sourceVersion && h.Operation == "FUND", cancellationToken);
            if (concurrent == null) throw;
            if (!string.Equals(concurrent.PayloadHash, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("A concurrent funding handoff was created for this source version with a different payload.", ex);
            return concurrent;
        }
        await tx.CommitAsync(cancellationToken);
        return handoff;
    }

    private async Task LockEndOfServiceSettlementAsync(long settlementId, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT ID FROM HR_END_OF_SERVICE_SETTLEMENT WHERE ID = :p_id FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "p_id";
        parameter.DbType = System.Data.DbType.Int64;
        parameter.Value = settlementId;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || result == DBNull.Value)
            throw new KeyNotFoundException("End of service settlement not found in active scope.");

        var tracked = db.EndOfServiceSettlements.Local.FirstOrDefault(x => x.Id == settlementId);
        if (tracked != null)
        {
            var entry = db.Entry(tracked);
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Cannot create an end of service payout handoff with pending local settlement changes.");
            await entry.ReloadAsync(cancellationToken);
        }
    }

    public async Task ConfirmLoanFundingAsync(long handoffId, decimal fundedAmount, string currency, long fundingVoucherId, string eventKey, string externalReference, string user, CancellationToken cancellationToken = default)
    {
        currency = Normalize(currency); eventKey = eventKey?.Trim() ?? string.Empty; externalReference = externalReference?.Trim() ?? string.Empty;
        if (fundedAmount <= 0 || fundingVoucherId <= 0 || eventKey.Length is < 8 or > 160 || externalReference.Length is < 1 or > 200)
            throw new ArgumentException("A positive funded amount, posted Accounting Payment Voucher ID, unique event key, and external disbursement reference are required.");
        if (db.Database.CurrentTransaction == null && unitOfWork != null)
        {
            var range = await ResolveFundingPayrollLockRangeAsync(handoffId, cancellationToken);
            await unitOfWork.ExecuteForEmployeeDatesAsync(range.EmployeeCode, range.StartDate, range.EndDate, async () =>
            {
                await ConfirmLoanFundingCoreAsync(handoffId, fundedAmount, currency, fundingVoucherId, eventKey, externalReference, user, cancellationToken);
                return true;
            }, cancellationToken);
            return;
        }
        await ConfirmLoanFundingCoreAsync(handoffId, fundedAmount, currency, fundingVoucherId, eventKey, externalReference, user, cancellationToken);
    }

    private async Task ConfirmLoanFundingCoreAsync(long handoffId, decimal fundedAmount, string currency, long fundingVoucherId, string eventKey, string externalReference, string user, CancellationToken cancellationToken)
    {
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken) ?? throw new KeyNotFoundException("Funding handoff not found.");
        if (handoff.SourceType is not ("LOAN" or "ADVANCE") || handoff.Operation != "FUND")
            throw new InvalidOperationException("Only a loan/advance funding request can receive a disbursement receipt.");
        var line = handoff.Lines.SingleOrDefault(x => x.LineType == "PRINCIPAL")
            ?? throw new InvalidOperationException("The funding handoff is missing its principal line.");
        if (handoff.Lines.Count != 1 || line.BranchId != handoff.BranchId ||
            fundedAmount != line.Amount || Normalize(currency) != ResolveHandoffCurrency(handoff.Lines))
            throw new InvalidOperationException("Funding receipt must match the single HR-approved principal line's amount, branch, and currency; partial funding is disabled until a company policy is configured.");
        var prior = await db.AccountingLoanFundingReceipts.SingleOrDefaultAsync(x => x.HandoffId == handoffId || x.EventKey == eventKey || x.ExternalReference == externalReference, cancellationToken);
        if (prior != null)
        {
            if (prior.HandoffId == handoffId && prior.SourceType == handoff.SourceType && prior.SourceId == handoff.SourceId &&
                prior.SourceVersion == handoff.SourceVersion && prior.EventKey == eventKey && prior.ExternalReference == externalReference &&
                prior.FundingVoucherId == fundingVoucherId && prior.FundedAmount == fundedAmount && Normalize(prior.CurrencyCode) == currency)
            {
                if (tx != null) await tx.RollbackAsync(cancellationToken);
                return;
            }
            throw new InvalidOperationException("A conflicting funding receipt already exists.");
        }
        if (handoff.DeliveryStatus != "ACCEPTED")
            throw new InvalidOperationException("Only an Accounting-accepted funding request can receive a new disbursement receipt.");
        var currencyMatches = await db.SysCurrencies.Where(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == currency)
            .Take(2).ToListAsync(cancellationToken);
        if (currencyMatches.Count != 1) throw new InvalidOperationException("Funding currency does not resolve to one unique ERP currency.");
        var accountingCurrency = currencyMatches[0];
        var fundingVoucherDto = await paymentVouchers.GetPaymentVoucherByIdAsync(fundingVoucherId, cancellationToken);
        var fundingVoucher = await vouchers.GetByIdAsync(fundingVoucherId, cancellationToken)
            ?? throw new KeyNotFoundException("Funding Payment Voucher was not found in Accounting.");
        var voucherReferencesHandoff = (fundingVoucherDto.Description?.Contains(handoff.HandoffReference, StringComparison.OrdinalIgnoreCase) ?? false) ||
            string.Equals(fundingVoucherDto.TransferReference?.Trim(), externalReference, StringComparison.OrdinalIgnoreCase);
        if (fundingVoucherDto.VoucherId != fundingVoucherId || fundingVoucherDto.Status != 3 || fundingVoucherDto.BranchId != handoff.BranchId ||
            fundingVoucherDto.TotalAmount != fundedAmount ||
            fundingVoucherDto.CurrencyId != accountingCurrency.Id ||
            !voucherReferencesHandoff ||
            fundingVoucher.Status != 3 || fundingVoucher.IsReversed || !fundingVoucher.PostDate.HasValue ||
            fundingVoucher.BranchId != handoff.BranchId || fundingVoucher.VoucherType is not (3 or 103) ||
            fundingVoucher.TotalAmount != fundedAmount ||
            fundingVoucher.Details.Count == 0 || fundingVoucher.Details.Any(d => d.CurrencyId != accountingCurrency.Id || d.BranchId != handoff.BranchId) ||
            fundingVoucher.Details.Sum(d => d.Debit) != fundedAmount || fundingVoucher.Details.Sum(d => d.Credit) != fundedAmount ||
            fundingVoucher.Details.Sum(d => d.LocalDebit) != fundingVoucher.TotalLocalDebit ||
            fundingVoucher.Details.Sum(d => d.LocalCredit) != fundingVoucher.TotalLocalCredit ||
            fundingVoucher.TotalLocalDebit != fundingVoucher.TotalLocalCredit)
            throw new InvalidOperationException("Funding requires a posted, unreversed Accounting Payment Voucher linked by handoff reference or transfer reference and matching the handoff branch, currency, and exact funded amount.");
        if (await db.AccountingLoanFundingReceipts.AnyAsync(x => x.FundingVoucherId == fundingVoucherId, cancellationToken))
            throw new InvalidOperationException("This Accounting Payment Voucher is already linked to another funding receipt.");
        if (!long.TryParse(handoff.SourceId, out var sourceId)) throw new InvalidOperationException("Funding source ID is invalid.");
        if (handoff.SourceType == "LOAN")
        {
            var loan = await db.EmployeeLoans.Include(x => x.Schedules).SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken) ?? throw new KeyNotFoundException("Linked loan is missing.");
            var expectedUnfundedBalance = handoff.SourceVersion == 1 ? fundedAmount : 0m;
            if (loan.Status != "APPROVED" || Normalize(loan.CurrencyCode) != currency || loan.PrincipalAmount != fundedAmount ||
                loan.TotalPaidAmount != 0 || loan.RemainingBalance != expectedUnfundedBalance ||
                loan.Schedules.Any(s => s.PaidAmount != 0 || s.CarriedForwardAmount != 0 || s.PayrollRunLineId.HasValue || s.ReservedByHandoffId.HasValue || s.Status != "PENDING"))
                throw new InvalidOperationException("Loan has changed since the approved funding snapshot or prior funding reversal.");
            loan.Status = "ACTIVE"; loan.RemainingBalance = fundedAmount; loan.UpdateUser = user; loan.UpdateDate = DateTime.UtcNow;
        }
        else
        {
            var advance = await db.EmployeeAdvances.SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken) ?? throw new KeyNotFoundException("Linked advance is missing.");
            var expectedUnfundedBalance = handoff.SourceVersion == 1 ? fundedAmount : 0m;
            if (advance.Status != "APPROVED" || Normalize(advance.CurrencyCode) != currency || advance.AdvanceAmount != fundedAmount ||
                advance.DeductedAmount != 0 || advance.RemainingBalance != expectedUnfundedBalance || advance.ReservedByHandoffId.HasValue)
                throw new InvalidOperationException("Advance has changed since the approved funding snapshot or prior funding reversal.");
            advance.Status = "ACTIVE"; advance.RemainingBalance = fundedAmount; advance.UpdateUser = user; advance.UpdateDate = DateTime.UtcNow;
        }
        db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
        {
            HandoffId = handoff.Id, SourceType = handoff.SourceType, SourceId = handoff.SourceId, SourceVersion = handoff.SourceVersion,
            FundedAmount = fundedAmount, CurrencyCode = currency, FundingVoucherId = fundingVoucherId, EventKey = eventKey, ExternalReference = externalReference,
            ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = user
        });
        handoff.DeliveryStatus = "POSTED";
        handoff.Transitions.Add(new HrAccountingHandoffTransition { FromStatus = "ACCEPTED", ToStatus = "POSTED", Actor = user, Reason = "ACCOUNTING_DISBURSEMENT_CONFIRMED", OccurredAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
    }

    public async Task<long> ReverseLoanFundingAsync(long handoffId, string reason, string eventKey, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        eventKey = eventKey?.Trim() ?? string.Empty;
        externalReference = externalReference?.Trim() ?? string.Empty;
        evidenceReference = evidenceReference?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1000 || eventKey.Length is < 8 or > 160 || externalReference.Length is < 1 or > 200 || evidenceReference.Length is < 1 or > 500)
            throw new ArgumentException("A reversal reason, unique event key, external reversal reference, and evidence reference are required.");
        if (db.Database.CurrentTransaction == null && unitOfWork != null)
        {
            var range = await ResolveFundingPayrollLockRangeAsync(handoffId, cancellationToken);
            return await unitOfWork.ExecuteForEmployeeDatesAsync(range.EmployeeCode, range.StartDate, range.EndDate,
                () => ReverseLoanFundingCoreAsync(handoffId, reason, eventKey, externalReference, evidenceReference, user, cancellationToken), cancellationToken);
        }
        return await ReverseLoanFundingCoreAsync(handoffId, reason, eventKey, externalReference, evidenceReference, user, cancellationToken);
    }

    private async Task<long> ReverseLoanFundingCoreAsync(long handoffId, string reason, string eventKey, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken)
    {
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Funding handoff not found.");
        if (handoff.SourceType is not ("LOAN" or "ADVANCE") || handoff.Operation != "FUND")
            throw new InvalidOperationException("Only a loan or advance funding handoff can be reversed here.");
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Funding handoff has no branch.");
        var receipt = await db.AccountingLoanFundingReceipts.SingleOrDefaultAsync(r => r.HandoffId == handoffId, cancellationToken)
            ?? throw new InvalidOperationException("Funding reversal requires a posted Accounting funding receipt.");
        if (receipt.FundingVoucherId is not > 0)
            throw new InvalidOperationException("This legacy funding receipt has no linked Payment Voucher; automatic reversal is blocked.");

        if (handoff.DeliveryStatus == "REVERSED")
        {
            if (receipt.ReversalReason != reason || receipt.ReversalEventKey != eventKey ||
                receipt.ReversalExternalReference != externalReference || receipt.ReversalEvidenceReference != evidenceReference)
                throw new InvalidOperationException("A reversed funding handoff is immutable.");
            var priorReversal = await GetVerifiedFundingReversalAsync(branchId, receipt.FundingVoucherId.Value, receipt.ReversalVoucherId, cancellationToken);
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            return priorReversal.Id;
        }
        if (handoff.DeliveryStatus != "POSTED")
            throw new InvalidOperationException("Only a funding handoff with a confirmed disbursement receipt can be reversed.");
        if (receipt.ReversalVoucherId.HasValue || receipt.ReversalEventKey != null || receipt.ReversedAtUtc.HasValue)
            throw new InvalidOperationException("Funding reversal receipt conflicts with the current handoff state.");
        if (await db.AccountingLoanFundingReceipts.AnyAsync(r => r.ReversalEventKey == eventKey || r.ReversalExternalReference == externalReference, cancellationToken))
            throw new InvalidOperationException("The reversal event key or external reference is already used by another funding reversal.");

        var originalVoucher = await vouchers.GetByIdAsync(receipt.FundingVoucherId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("Linked funding Payment Voucher is missing from Accounting.");
        var receiptCurrency = Normalize(receipt.CurrencyCode);
        var fundingCurrencyMatches = await db.SysCurrencies.Where(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == receiptCurrency)
            .Take(2).ToListAsync(cancellationToken);
        if (fundingCurrencyMatches.Count != 1) throw new InvalidOperationException("Funding receipt currency no longer resolves to one ERP currency.");
        var fundingVoucherDto = await paymentVouchers.GetPaymentVoucherByIdAsync(originalVoucher.Id, cancellationToken);
        if (fundingVoucherDto.VoucherId != originalVoucher.Id || fundingVoucherDto.BranchId != branchId ||
            fundingVoucherDto.CurrencyId != fundingCurrencyMatches[0].Id || fundingVoucherDto.TotalAmount != receipt.FundedAmount ||
            originalVoucher.TotalAmount != receipt.FundedAmount || originalVoucher.BranchId != branchId ||
            originalVoucher.Status != 3 || !originalVoucher.PostDate.HasValue ||
            originalVoucher.VoucherType is not (3 or 103) || originalVoucher.Details.Count == 0 ||
            fundingVoucherDto.SettledBills.Count != 0 || originalVoucher.Details.Any(d => d.CurrencyId != fundingCurrencyMatches[0].Id || d.BranchId != branchId) ||
            originalVoucher.Details.Sum(d => d.Debit) != receipt.FundedAmount || originalVoucher.Details.Sum(d => d.Credit) != receipt.FundedAmount ||
            originalVoucher.TotalLocalDebit != originalVoucher.TotalLocalCredit ||
            originalVoucher.Details.Sum(d => d.LocalDebit) != originalVoucher.TotalLocalDebit ||
            originalVoucher.Details.Sum(d => d.LocalCredit) != originalVoucher.TotalLocalCredit)
            throw new InvalidOperationException("Linked funding Payment Voucher no longer matches the posted receipt.");

        EmployeeLoan? loan = null;
        EmployeeAdvance? advance = null;
        if (!long.TryParse(receipt.SourceId, out var sourceId) || receipt.SourceType != handoff.SourceType || receipt.SourceVersion != handoff.SourceVersion)
            throw new InvalidOperationException("Funding receipt does not match its source version.");
        if (receipt.SourceType == "LOAN")
        {
            loan = await db.EmployeeLoans.Include(l => l.Schedules).SingleOrDefaultAsync(l => l.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Linked loan is missing.");
            if (loan.Status != "ACTIVE" || Normalize(loan.CurrencyCode) != receiptCurrency || loan.PrincipalAmount != receipt.FundedAmount ||
                loan.TotalPaidAmount != 0 || loan.RemainingBalance != receipt.FundedAmount ||
                loan.Schedules.Any(s => s.PaidAmount != 0 || s.CarriedForwardAmount != 0 || s.PayrollRunLineId.HasValue ||
                    s.ReservedByHandoffId.HasValue || s.Status != "PENDING"))
                throw new InvalidOperationException("Loan has repayments, reservations, schedule changes, or balance changes; reverse later payroll settlements first.");
        }
        else
        {
            advance = await db.EmployeeAdvances.SingleOrDefaultAsync(a => a.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Linked advance is missing.");
            if (advance.Status != "ACTIVE" || Normalize(advance.CurrencyCode) != receiptCurrency || advance.AdvanceAmount != receipt.FundedAmount ||
                advance.DeductedAmount != 0 || advance.RemainingBalance != receipt.FundedAmount || advance.ReservedByHandoffId.HasValue)
                throw new InvalidOperationException("Advance has deductions, reservations, or balance changes; reverse later payroll settlements first.");
        }

        var priorAccountingReversals = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", originalVoucher.Id, cancellationToken);
        GlVoucherHeader reversalVoucher;
        if (originalVoucher.IsReversed)
        {
            if (priorAccountingReversals.Count != 1 || !IsExactAccountingReversal(originalVoucher, priorAccountingReversals[0]))
                throw new InvalidOperationException("Funding Payment Voucher is already reversed but its Accounting reversal cannot be verified.");
            reversalVoucher = priorAccountingReversals[0];
        }
        else
        {
            if (priorAccountingReversals.Count != 0) throw new InvalidOperationException("A reversal voucher exists while the funding Payment Voucher is not marked reversed.");
            await glVouchers.ReverseVoucherAsync(originalVoucher.Id, user, reason, cancellationToken);
            reversalVoucher = await GetVerifiedFundingReversalAsync(branchId, originalVoucher.Id, null, cancellationToken);
        }

        if (loan != null)
        {
            loan.Status = "APPROVED";
            loan.RemainingBalance = 0;
            loan.UpdateUser = user;
            loan.UpdateDate = DateTime.UtcNow;
        }
        else if (advance != null)
        {
            advance.Status = "APPROVED";
            advance.RemainingBalance = 0;
            advance.UpdateUser = user;
            advance.UpdateDate = DateTime.UtcNow;
        }
        receipt.ReversalVoucherId = reversalVoucher.Id;
        receipt.ReversalEventKey = eventKey;
        receipt.ReversalReason = reason;
        receipt.ReversalExternalReference = externalReference;
        receipt.ReversalEvidenceReference = evidenceReference;
        receipt.ReversedAtUtc = DateTime.UtcNow;
        receipt.ReversedBy = user;
        handoff.DeliveryStatus = "REVERSED";
        handoff.Transitions.Add(new HrAccountingHandoffTransition
        {
            FromStatus = "POSTED", ToStatus = "REVERSED", Actor = user, Reason = reason, OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return reversalVoucher.Id;
    }

    private async Task<(string EmployeeCode, DateTime StartDate, DateTime EndDate)> ResolveFundingPayrollLockRangeAsync(long handoffId, CancellationToken cancellationToken)
    {
        var handoff = await db.HrAccountingHandoffs.AsNoTracking().SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Funding handoff not found.");
        if (handoff.SourceType is not ("LOAN" or "ADVANCE") || handoff.Operation != "FUND" ||
            !long.TryParse(handoff.SourceId, out var sourceId))
            throw new InvalidOperationException("Only a valid loan or advance funding handoff can use payroll input locking.");

        string employeeCode;
        DateTime start;
        DateTime end;
        if (handoff.SourceType == "LOAN")
        {
            var loan = await db.EmployeeLoans.AsNoTracking().SingleOrDefaultAsync(l => l.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Funding loan source not found.");
            employeeCode = loan.EmployeeCode;
            var payPeriods = await db.LoanRepaymentSchedules.AsNoTracking().Where(s => s.EmployeeLoanId == loan.Id)
                .Select(s => s.PayPeriod).Distinct().ToListAsync(cancellationToken);
            var bounds = payPeriods.Select(ParsePayrollMonth).OrderBy(d => d).ToArray();
            start = bounds.Length == 0 ? loan.StartDate.Date : bounds[0];
            end = bounds.Length == 0 ? (loan.EndDate ?? loan.StartDate).Date : bounds[^1].AddMonths(1).AddDays(-1);
        }
        else
        {
            var advance = await db.EmployeeAdvances.AsNoTracking().SingleOrDefaultAsync(a => a.Id == sourceId, cancellationToken)
                ?? throw new KeyNotFoundException("Funding advance source not found.");
            employeeCode = advance.EmployeeCode;
            start = ParsePayrollMonth(advance.TargetPayPeriod);
            end = start.AddMonths(1).AddDays(-1);
        }

        if (string.IsNullOrWhiteSpace(employeeCode) || end.Date < start.Date)
            throw new InvalidOperationException("The funding source does not have a valid employee payroll date range.");
        return (employeeCode, start.Date, end.Date);
    }

    private static DateTime ParsePayrollMonth(string periodCode)
    {
        if (DateTime.TryParseExact(periodCode, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var month))
            return new DateTime(month.Year, month.Month, 1);
        throw new InvalidOperationException("Loan or advance funding requires payroll periods in yyyy-MM format so the affected payroll inputs can be locked safely.");
    }

    private async Task<GlVoucherHeader> GetVerifiedFundingReversalAsync(long branchId, long fundingVoucherId, long? expectedReversalVoucherId, CancellationToken cancellationToken)
    {
        var original = await vouchers.GetByIdAsync(fundingVoucherId, cancellationToken)
            ?? throw new KeyNotFoundException("Funding Payment Voucher is missing from Accounting.");
        var reversals = await vouchers.GetBySourceAsync(branchId, "GL_REVERSAL", fundingVoucherId, cancellationToken);
        if (!original.IsReversed || reversals.Count != 1 || expectedReversalVoucherId.HasValue && reversals[0].Id != expectedReversalVoucherId.Value ||
            !IsExactAccountingReversal(original, reversals[0]))
            throw new InvalidOperationException("The stored funding reversal does not exactly offset its posted Accounting Payment Voucher.");
        return reversals[0];
    }

    public async Task<IReadOnlyList<ThinkOnErp.Application.DTOs.Hr.AccountingHandoffInboxDto>> GetAccountingInboxAsync(CancellationToken cancellationToken = default)
    {
        var handoffs = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Lines).Include(h => h.Transitions)
            .Where(h => new[] { "DELIVERED", "RETURNED", "ACCEPTED", "WITHDRAWN" }.Contains(h.DeliveryStatus))
            .OrderBy(h => h.CreatedAtUtc).ToListAsync(cancellationToken);
        return handoffs.Select(h =>
        {
            var latest = h.Transitions.OrderByDescending(t => t.OccurredAtUtc).FirstOrDefault();
            return new ThinkOnErp.Application.DTOs.Hr.AccountingHandoffInboxDto(
                h.Id, db.HrScopeCompanyId, h.BranchId, h.SourceType, h.SourceId, h.SourceVersion, h.Operation,
                h.HandoffReference, h.PayloadHash, h.DeliveryStatus, h.PayPeriod, h.SourceApprovedAtUtc, h.CreatedAtUtc, h.CreatedBy,
                latest?.Reason, latest?.OccurredAtUtc,
                h.Lines.Select(l => new ThinkOnErp.Application.DTOs.Hr.AccountingHandoffLineDto(
                    l.LineType, l.CurrencyCode, l.Amount, l.BranchId, l.CostCenterReference, l.SourceReference)).ToList());
        }).ToList();
    }

    public async Task<AccountingHandoffPaymentSummaryDto?> GetAccountingPaymentSummaryAsync(long handoffId, CancellationToken cancellationToken = default)
    {
        var handoff = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Lines)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken);
        if (handoff == null) return null;
        if (handoff.SourceType != "PAYROLL" || handoff.Operation != "SUBMIT")
            throw new InvalidOperationException("Payment status is available only for payroll submission handoffs.");

        var payableLines = handoff.Lines.Where(l => l.LineType == "CONTROL:NET_PAY").ToArray();
        if (payableLines.Length == 0)
            throw new InvalidOperationException("Payroll handoff has no net-pay amount.");
        var currency = ResolveHandoffCurrency(handoff.Lines);
        var payable = payableLines.Sum(l => l.Amount);
        var branchId = handoff.BranchId ?? throw new InvalidOperationException("Payroll handoff has no branch.");
        var payments = await db.AccountingHandoffPayments.AsNoTracking().Where(p => p.HandoffId == handoffId)
            .OrderBy(p => p.PaymentSequence).ToListAsync(cancellationToken);

        var items = new List<AccountingHandoffPaymentDto>(payments.Count);
        foreach (var payment in payments)
        {
            if (Normalize(payment.CurrencyCode) != currency)
                throw new InvalidOperationException("Payment history contains a currency that does not match the payroll handoff.");
            var linkedVouchers = await vouchers.GetBySourceAsync(branchId, "HR_PAYMENT", payment.Id, cancellationToken);
            if (linkedVouchers.Count > 1)
                throw new InvalidOperationException("More than one Payment Voucher is linked to a payment instruction.");
            items.Add(new AccountingHandoffPaymentDto(
                payment.Id, payment.PaymentSequence, payment.CurrencyCode, payment.Amount, payment.Method,
                payment.PaymentMethodCode, payment.Status, linkedVouchers.SingleOrDefault()?.Id,
                payment.ExternalReference, payment.EvidenceReference, payment.CreatedAtUtc, payment.CreatedBy,
                payment.ConfirmedAtUtc, payment.ConfirmedBy, payment.FailureReason, payment.FailedAtUtc, payment.FailedBy,
                payment.ReversalReason, payment.ReversalExternalReference, payment.ReversalEvidenceReference,
                payment.ReversedAtUtc, payment.ReversedBy, payment.ReversalVoucherId));
        }

        var prepared = payments.Where(p => p.Status == "PREPARED").Sum(p => p.Amount);
        var paid = payments.Where(p => p.Status == "CONFIRMED").Sum(p => p.Amount);
        var reversed = payments.Where(p => p.Status == "REVERSED").Sum(p => p.Amount);
        return new AccountingHandoffPaymentSummaryDto(
            handoff.Id, handoff.HandoffReference, handoff.DeliveryStatus, currency, payable,
            prepared, paid, reversed, payable - prepared - paid, items);
    }

    public async Task<PayrollHandoffPaymentStatusDto?> GetPayrollHandoffPaymentStatusAsync(long payrollRunId, CancellationToken cancellationToken = default)
    {
        var handoffId = await db.HrAccountingHandoffs.AsNoTracking()
            .Where(h => h.SourceType == "PAYROLL" && h.SourceId == payrollRunId.ToString() && h.Operation == "SUBMIT")
            .Select(h => (long?)h.Id).SingleOrDefaultAsync(cancellationToken);
        if (!handoffId.HasValue) return null;
        var summary = await GetAccountingPaymentSummaryAsync(handoffId.Value, cancellationToken);
        if (summary == null) return null;

        var status = summary.PaidAmount >= summary.PayableAmount && summary.PaidAmount > 0
            ? "PAID"
            : summary.PaidAmount > 0
                ? "PARTIAL"
                : summary.ReversedAmount > 0 && summary.PreparedAmount == 0
                    ? "REVERSED"
                    : "UNPAID";
        return new PayrollHandoffPaymentStatusDto(
            summary.HandoffId, summary.HandoffReference, summary.CurrencyCode, summary.PayableAmount,
            summary.PreparedAmount, summary.PaidAmount, summary.ReversedAmount, summary.RemainingAmount, status);
    }

    public async Task<ThinkOnErp.Application.DTOs.Hr.PayrollHandoffStatusDto?> GetPayrollHandoffStatusAsync(long payrollRunId, CancellationToken cancellationToken = default)
    {
        var handoff = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.SourceType == "PAYROLL" && h.SourceId == payrollRunId.ToString() && h.Operation == "SUBMIT", cancellationToken);
        if (handoff == null) return null;
        var latest = handoff.Transitions.OrderByDescending(t => t.OccurredAtUtc).FirstOrDefault();
        return ToPayrollHandoffStatus(handoff, latest);
    }

    public async Task<ThinkOnErp.Application.DTOs.Hr.PayrollHandoffStatusDto?> WithdrawPayrollHandoffAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is < 1 or > 1000) throw new ArgumentException("A withdrawal reason of 1..1000 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).Include(h => h.OutboxMessages)
            .SingleOrDefaultAsync(h => h.SourceType == "PAYROLL" && h.SourceId == payrollRunId.ToString() && h.Operation == "SUBMIT", cancellationToken);
        if (handoff == null) { await tx.RollbackAsync(cancellationToken); return null; }
        var latest = handoff.Transitions.OrderByDescending(t => t.OccurredAtUtc).FirstOrDefault();
        if (handoff.DeliveryStatus == "WITHDRAWN")
        {
            if (latest?.Actor == user && latest.Reason == reason) { await tx.RollbackAsync(cancellationToken); return ToPayrollHandoffStatus(handoff, latest); }
            throw new InvalidOperationException("Payroll handoff has already been withdrawn and cannot be changed.");
        }
        if (handoff.DeliveryStatus is not ("QUEUED" or "DELIVERED"))
            throw new InvalidOperationException("Payroll handoff can be withdrawn only before Accounting accepts it.");

        var pendingSubmit = handoff.OutboxMessages.SingleOrDefault(m => m.EventType == "PAYROLL_SUBMIT" && m.Status == "PENDING");
        if (pendingSubmit != null)
        {
            pendingSubmit.Status = "CANCELLED";
            pendingSubmit.LastAttemptAtUtc = DateTime.UtcNow;
            pendingSubmit.LastError = "Withdrawn by HR before delivery.";
        }
        await ReleasePayrollReservationsAsync(handoff.Id, cancellationToken);
        var oldStatus = handoff.DeliveryStatus;
        handoff.DeliveryStatus = "WITHDRAWN";
        handoff.Transitions.Add(new HrAccountingHandoffTransition
        {
            FromStatus = oldStatus, ToStatus = "WITHDRAWN", Actor = user, Reason = reason, OccurredAtUtc = DateTime.UtcNow
        });
        var withdrawalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{handoff.PayloadHash}|WITHDRAWN|{user}|{reason}")));
        handoff.OutboxMessages.Add(new HrAccountingOutboxMessage
        {
            EventKey = $"{db.HrScopeCompanyId}:PAYROLL:{handoff.SourceId}:{handoff.SourceVersion}:WITHDRAW",
            EventType = "PAYROLL_WITHDRAW", PayloadHash = withdrawalHash, Status = "PENDING", CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return ToPayrollHandoffStatus(handoff, handoff.Transitions.OrderByDescending(t => t.OccurredAtUtc).First());
    }

    private static ThinkOnErp.Application.DTOs.Hr.PayrollHandoffStatusDto ToPayrollHandoffStatus(HrAccountingHandoff handoff, HrAccountingHandoffTransition? latest) =>
        new(handoff.Id, handoff.HandoffReference, handoff.SourceVersion, handoff.DeliveryStatus, handoff.PayPeriod,
            handoff.SourceApprovedAtUtc, handoff.CreatedAtUtc, latest?.OccurredAtUtc, latest?.Reason);

    public async Task<HrAccountingHandoff> ReviewAsync(long handoffId, bool accept, string? reason, string user, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim();
        if (!accept && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000))
            throw new ArgumentException("A return reason of 1..1000 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.Id == handoffId, cancellationToken)
            ?? throw new KeyNotFoundException("Handoff was not found in the Accounting scope.");
        if (handoff.DeliveryStatus != "DELIVERED")
            throw new InvalidOperationException("Only a delivered handoff can be reviewed.");
        var old = handoff.DeliveryStatus;
        handoff.DeliveryStatus = accept ? "ACCEPTED" : "RETURNED";
        handoff.Transitions.Add(new HrAccountingHandoffTransition
        {
            FromStatus = old, ToStatus = handoff.DeliveryStatus, Actor = user,
            Reason = accept ? null : reason, OccurredAtUtc = DateTime.UtcNow
        });
        if (!accept && handoff.SourceType == "PAYROLL" && handoff.Operation == "SUBMIT")
            await ReleasePayrollReservationsAsync(handoff.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return handoff;
    }

    public async Task<HrAccountingHandoff> CreateApprovedPayrollHandoffAsync(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        // PayrollService already owns the period-locking transaction while it approves the run.
        // Join it so approval, immutable handoff, reservations, and outbox either commit together or roll back together.
        await using var tx = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        var run = await db.PayrollRuns.Include(r => r.Lines).ThenInclude(l => l.Components)
            .SingleOrDefaultAsync(r => r.Id == payrollRunId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run was not found in the active HR scope.");
        if (run.Status != "APPROVED" || !run.ApprovalDate.HasValue)
            throw new InvalidOperationException("Only an approved payroll run can be handed to Accounting.");
        var branchId = run.BranchId ?? throw new InvalidOperationException("Payroll handoff requires a branch-specific run.");
        var branch = await db.SysBranches.SingleOrDefaultAsync(b => b.Id == branchId && b.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Payroll branch was not found or is inactive.");
        var companyId = branch.CompanyId ?? throw new InvalidOperationException("Payroll branch has no company owner.");
        if (db.HrScopeEnabled && companyId != db.HrScopeCompanyId)
            throw new UnauthorizedAccessException("Payroll branch is outside the active company scope.");
        if (run.Lines.Count == 0) throw new InvalidOperationException("An empty payroll run cannot be handed to Accounting.");
        foreach (var line in run.Lines)
            ValidatePayrollComponentBreakdown(line);
        var negativeComponents = run.Lines.SelectMany(line => line.Components)
            .Where(component => component.Amount < 0m).ToArray();
        if (negativeComponents.Any(component => Normalize(component.ComponentType) == "DEDUCTION"))
            throw new InvalidOperationException("Payroll run with a negative deduction component cannot be handed to Accounting.");
        if (negativeComponents.Length > 0)
            throw new InvalidOperationException("Payroll run with a negative earning or employer contribution component cannot be handed to Accounting.");

        var currencyGroups = run.Lines.GroupBy(l => Normalize(l.CurrencyCode)).ToArray();
        if (currencyGroups.Length != 1 || currencyGroups[0].Key.Length == 0)
            throw new InvalidOperationException("Payroll handoff requires one explicit currency across all lines.");
        var currency = currencyGroups[0].Key;
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["GROSS"] = run.Lines.Sum(l => l.GrossSalary),
            ["EMPLOYEE_SSC"] = run.Lines.Sum(l => l.SscEmployeeContrib),
            ["EMPLOYER_SSC"] = run.Lines.Sum(l => l.SscEmployerContrib),
            ["INCOME_TAX"] = run.Lines.Sum(l => l.IncomeTaxWithheld),
            ["NATIONAL_CONTRIBUTION"] = run.Lines.Sum(l => l.NationalContribWithheld),
            ["OTHER_DEDUCTIONS"] = run.Lines.Sum(l => l.OtherDeductions),
            ["NET_PAY"] = run.Lines.Sum(l => l.NetPay)
        };
        if (totals["GROSS"] != run.TotalGrossSalary || totals["NET_PAY"] != run.TotalNetSalary ||
            totals["EMPLOYEE_SSC"] != run.TotalEmployeeSsc || totals["EMPLOYER_SSC"] != run.TotalEmployerSsc ||
            totals["INCOME_TAX"] != run.TotalIncomeTax || totals["NATIONAL_CONTRIBUTION"] != run.TotalNationalContrib ||
            totals["OTHER_DEDUCTIONS"] != run.TotalOtherDeductions)
            throw new InvalidOperationException("Payroll header totals do not match its approved lines.");

        if (run.TotalNetSalary < 0 || run.Lines.Any(l => l.NetPay < 0))
            throw new InvalidOperationException("Payroll run with negative net pay cannot be handed to Accounting.");

        if (run.TotalGrossSalary < 0 || run.Lines.Any(l => l.GrossSalary < 0))
            throw new InvalidOperationException("Payroll run with negative gross salary cannot be handed to Accounting.");

        var existing = await db.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
            .SingleOrDefaultAsync(h => h.SourceType == "PAYROLL" &&
                h.SourceId == run.Id.ToString() && h.SourceVersion == run.CorrectionVersion && h.Operation == "SUBMIT", cancellationToken);
        if (existing != null) return existing;

        var lines = run.Lines.SelectMany(l => l.Components.Select(c => new
            {
                LineType = c.ComponentType + ":" + c.ComponentCode,
                c.Amount,
                BranchId = l.BranchId ?? branchId,
                CostCenterReference = l.CostCenterCode,
                SourceReference = "PAYROLL_LINE:" + l.Id
            }))
            .GroupBy(x => new { x.LineType, x.BranchId, x.CostCenterReference })
            .Select(g => new HrAccountingHandoffLine
            {
                LineType = g.Key.LineType,
                CurrencyCode = currency,
                Amount = g.Sum(x => x.Amount),
                BranchId = g.Key.BranchId,
                CostCenterReference = g.Key.CostCenterReference,
                SourceReference = "PAYROLL_RUN:" + run.Id
            }).ToList();
        foreach (var (type, amount) in totals)
            lines.Add(new HrAccountingHandoffLine { LineType = "CONTROL:" + type, CurrencyCode = currency, Amount = amount, BranchId = branchId, SourceReference = "PAYROLL_RUN:" + run.Id });

        var allocations = await BuildPayrollReservationPlanAsync(run, currency, cancellationToken);
        lines.AddRange(allocations.Select(a => new HrAccountingHandoffLine
        {
            LineType = "REFERENCE:" + a.Kind,
            CurrencyCode = currency,
            Amount = a.Amount,
            BranchId = branchId,
            SourceReference = $"{a.Kind}:{a.SourceId}:PAYROLL_LINE:{a.PayrollRunLineId}"
        }));
        lines = lines.OrderBy(l => l.LineType, StringComparer.Ordinal)
            .ThenBy(l => l.BranchId)
            .ThenBy(l => l.CostCenterReference, StringComparer.Ordinal)
            .ThenBy(l => l.SourceReference, StringComparer.Ordinal)
            .ToList();
        var payload = JsonSerializer.Serialize(new { run.Id, run.PayPeriod, run.ApprovalDate, currency, totals, Lines = lines.Select(l => new { l.LineType, l.Amount, l.BranchId, l.CostCenterReference, l.SourceReference }) });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var handoff = new HrAccountingHandoff
        {
            CompanyId = companyId, BranchId = branchId, SourceType = "PAYROLL", SourceId = run.Id.ToString(),
            SourceVersion = run.CorrectionVersion, Operation = "SUBMIT", PayPeriod = run.PayPeriod, SourceApprovedAtUtc = run.ApprovalDate,
            HandoffReference = $"HR-PAY-{companyId}-{run.Id}-{run.CorrectionVersion}",
            PayloadHash = hash, DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = user,
            Lines = lines,
            // Oracle stores empty strings as NULL, but FROM_STATUS is NOT NULL.
            // Persist an explicit initial state so the first payroll handoff is portable across providers.
            Transitions = [new HrAccountingHandoffTransition { FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = user, OccurredAtUtc = DateTime.UtcNow }]
        };
        handoff.OutboxMessages.Add(new HrAccountingOutboxMessage
        {
            EventKey = $"{companyId}:PAYROLL:{run.Id}:{run.CorrectionVersion}:SUBMIT",
            EventType = "PAYROLL_SUBMIT",
            PayloadHash = hash,
            Status = "PENDING",
            CreatedAtUtc = DateTime.UtcNow
        });
        db.HrAccountingHandoffs.Add(handoff);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var allocation in allocations)
            {
                if (allocation.Kind == "ADVANCE") allocation.Advance!.ReservedByHandoffId = handoff.Id;
                else allocation.Schedule!.ReservedByHandoffId = handoff.Id;
            }
            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is OracleException { Number: 1 })
        {
            if (tx == null) throw;
            await tx.RollbackAsync(cancellationToken);
            foreach (var allocation in allocations)
            {
                if (allocation.Kind == "ADVANCE")
                {
                    allocation.Advance!.ReservedByHandoffId = null;
                    db.Entry(allocation.Advance).Property(x => x.ReservedByHandoffId).IsModified = false;
                }
                else
                {
                    allocation.Schedule!.ReservedByHandoffId = null;
                    db.Entry(allocation.Schedule).Property(x => x.ReservedByHandoffId).IsModified = false;
                }
            }
            db.Entry(handoff).State = EntityState.Detached;
            foreach (var line in handoff.Lines) db.Entry(line).State = EntityState.Detached;
            foreach (var transition in handoff.Transitions) db.Entry(transition).State = EntityState.Detached;
            foreach (var outboxMessage in handoff.OutboxMessages) db.Entry(outboxMessage).State = EntityState.Detached;
            var raced = await db.HrAccountingHandoffs.AsNoTracking().Include(h => h.Lines).SingleOrDefaultAsync(h =>
                h.SourceType == "PAYROLL" && h.SourceId == run.Id.ToString() &&
                h.SourceVersion == run.CorrectionVersion && h.Operation == "SUBMIT", cancellationToken);
            if (raced == null) throw;
            if (!string.Equals(raced.PayloadHash, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("A concurrent payroll handoff was created for this source version with a different payload.", ex);
            return raced;
        }
        return handoff;
    }

    private sealed record PayrollReservation(string Kind, long SourceId, long PayrollRunLineId, decimal Amount, EmployeeAdvance? Advance, LoanRepaymentSchedule? Schedule);

    private async Task<List<PayrollReservation>> BuildPayrollReservationPlanAsync(PayrollRun run, string currency, CancellationToken cancellationToken)
    {
        var plan = new List<PayrollReservation>();
        var plannedAdvanceIds = new HashSet<long>();
        var plannedScheduleIds = new HashSet<long>();
        var plannedLoanAmounts = new Dictionary<long, decimal>();
        foreach (var line in run.Lines)
        {
            var deduction = line.Components.Where(c => Normalize(c.ComponentType) == "DEDUCTION" &&
                    Normalize(c.ComponentCode) == "LOAN_DEDUCTION")
                .Sum(c => c.Amount);
            if (deduction <= 0) continue;
            var remaining = deduction;
            var advances = await db.EmployeeAdvances.Where(a => a.EmployeeCode == line.EmployeeCode && a.TargetPayPeriod == run.PayPeriod && a.Status == "ACTIVE" && a.RemainingBalance > 0 && a.ReservedByHandoffId == null)
                .OrderByDescending(a => a.CreationDate).ToListAsync(cancellationToken);
            foreach (var advance in advances)
            {
                if (plannedAdvanceIds.Contains(advance.Id)) continue;
                if (advance.CurrencyCode == null || Normalize(advance.CurrencyCode) != currency) throw new InvalidOperationException($"Active advance {advance.Id} needs an explicit matching currency before payroll handoff.");
                var amount = Math.Min(remaining, advance.RemainingBalance);
                if (amount > 0)
                {
                    plan.Add(new PayrollReservation("ADVANCE", advance.Id, line.Id, amount, advance, null));
                    plannedAdvanceIds.Add(advance.Id);
                    remaining -= amount;
                }
                if (remaining == 0) break;
            }
            if (remaining > 0)
            {
                var schedules = await db.LoanRepaymentSchedules.Include(s => s.EmployeeLoan)
                    .Where(s => string.Compare(s.PayPeriod, run.PayPeriod) <= 0 && s.EmployeeLoan != null && s.EmployeeLoan.EmployeeCode == line.EmployeeCode && s.EmployeeLoan.Status == "ACTIVE" &&
                        (s.Status == "PENDING" || s.Status == "PARTIAL" || s.Status == "SKIPPED") && s.ReservedByHandoffId == null)
                    .OrderBy(s => s.PayPeriod).ThenBy(s => s.InstallmentNo).ToListAsync(cancellationToken);
                foreach (var schedule in schedules)
                {
                    if (plannedScheduleIds.Contains(schedule.Id)) continue;
                    if (schedule.EmployeeLoan!.CurrencyCode == null || Normalize(schedule.EmployeeLoan.CurrencyCode) != currency) throw new InvalidOperationException($"Active loan {schedule.EmployeeLoanId} needs an explicit matching currency before payroll handoff.");
                    var due = Math.Max(0m, schedule.ScheduledAmount - schedule.PaidAmount);
                    var loanRemaining = Math.Max(0m, schedule.EmployeeLoan.RemainingBalance - plannedLoanAmounts.GetValueOrDefault(schedule.EmployeeLoanId));
                    var amount = Math.Min(remaining, Math.Min(due, loanRemaining));
                    if (amount > 0)
                    {
                        plan.Add(new PayrollReservation("SCHEDULE", schedule.Id, line.Id, amount, null, schedule));
                        plannedScheduleIds.Add(schedule.Id);
                        plannedLoanAmounts[schedule.EmployeeLoanId] = plannedLoanAmounts.GetValueOrDefault(schedule.EmployeeLoanId) + amount;
                        remaining -= amount;
                    }
                    if (remaining == 0) break;
                }
            }
            if (remaining != 0) throw new InvalidOperationException($"Payroll line {line.Id} has {deduction} of loan deduction that cannot be linked to eligible unreserved advances or installments.");
        }
        return plan;
    }

    private async Task ReleasePayrollReservationsAsync(long handoffId, CancellationToken cancellationToken)
    {
        var advances = await db.EmployeeAdvances.Where(a => a.ReservedByHandoffId == handoffId).ToListAsync(cancellationToken);
        var schedules = await db.LoanRepaymentSchedules.Where(s => s.ReservedByHandoffId == handoffId).ToListAsync(cancellationToken);
        foreach (var advance in advances) advance.ReservedByHandoffId = null;
        foreach (var schedule in schedules) schedule.ReservedByHandoffId = null;
    }

    private sealed record PayrollSettlementSnapshot(
        string Kind, long SourceId, long PayrollRunLineId, decimal Amount, string PriorStatus,
        decimal? PriorPaidAmount, decimal? PriorCarriedForwardAmount, DateTime? PriorPaidDate, DateTime? AppliedPaidDate,
        long? PriorSchedulePayrollLineId,
        decimal? PriorDeductedAmount, decimal? PriorRemainingBalance,
        decimal? PriorLoanTotalPaidAmount, decimal? PriorLoanRemainingBalance, string? PriorLoanStatus);

    private static bool MatchesSettlementSnapshot(HrAccountingHandoffLine line, PayrollSettlementSnapshot snapshot)
    {
        var parts = (line.SourceReference ?? string.Empty).Split(':');
        return parts.Length == 4 && parts[2] == "PAYROLL_LINE" &&
            long.TryParse(parts[1], out var sourceId) && long.TryParse(parts[3], out var payrollLineId) &&
            ((line.LineType == "REFERENCE:ADVANCE" && snapshot.Kind == "ADVANCE") ||
             (line.LineType == "REFERENCE:SCHEDULE" && snapshot.Kind == "SCHEDULE")) &&
            sourceId == snapshot.SourceId && payrollLineId == snapshot.PayrollRunLineId && line.Amount == snapshot.Amount;
    }

    private async Task<string> SettlePayrollReservationsAsync(HrAccountingHandoff handoff, string user, CancellationToken cancellationToken)
    {
        if (!long.TryParse(handoff.SourceId, out var runId)) throw new InvalidOperationException("Payroll handoff source ID is invalid.");
        var sourceRun = await db.PayrollRuns.AsNoTracking().SingleOrDefaultAsync(run => run.Id == runId, cancellationToken)
            ?? throw new InvalidOperationException("The source payroll run for this settlement no longer exists.");
        if (sourceRun.Status != "APPROVED" || sourceRun.BranchId != handoff.BranchId ||
            !string.Equals(sourceRun.PayPeriod, handoff.PayPeriod, StringComparison.Ordinal) ||
            sourceRun.CorrectionVersion != handoff.SourceVersion ||
            handoff.SourceApprovedAtUtc.HasValue && sourceRun.ApprovalDate != handoff.SourceApprovedAtUtc)
            throw new InvalidOperationException("The source payroll run no longer matches the handoff branch, period, version, and approval snapshot.");
        var references = handoff.Lines.Where(l => l.LineType is "REFERENCE:ADVANCE" or "REFERENCE:SCHEDULE").ToArray();
        var handoffCurrency = ResolveHandoffCurrency(handoff.Lines);
        if (references.Any(reference => reference.Amount <= 0m || reference.BranchId != handoff.BranchId ||
                Normalize(reference.CurrencyCode) != handoffCurrency))
            throw new InvalidOperationException("Payroll settlement references must have a positive amount and match the handoff branch and currency.");
        var advances = await db.EmployeeAdvances.Where(a => a.ReservedByHandoffId == handoff.Id).ToDictionaryAsync(a => a.Id, cancellationToken);
        var schedules = await db.LoanRepaymentSchedules.Include(s => s.EmployeeLoan).Where(s => s.ReservedByHandoffId == handoff.Id).ToDictionaryAsync(s => s.Id, cancellationToken);
        var allocations = new List<(HrAccountingHandoffLine Line, string Kind, long Id, long PayrollLineId)>();
        foreach (var reference in references)
        {
            var parts = (reference.SourceReference ?? string.Empty).Split(':');
            if (parts.Length != 4 || parts[2] != "PAYROLL_LINE" || !long.TryParse(parts[1], out var sourceId) || sourceId <= 0 ||
                !long.TryParse(parts[3], out var payrollLineId) || payrollLineId <= 0)
                throw new InvalidOperationException("Payroll reservation reference is malformed.");
            allocations.Add((reference, parts[0], sourceId, payrollLineId));
        }
        if (allocations.GroupBy(allocation => (allocation.Kind, allocation.Id)).Any(group => group.Count() != 1))
            throw new InvalidOperationException("A payroll reservation source must appear only once in the settlement references.");
        if (allocations.Any(a => a.Kind == "ADVANCE" ? !advances.ContainsKey(a.Id) : a.Kind != "SCHEDULE" || !schedules.ContainsKey(a.Id)))
            throw new InvalidOperationException("A payroll reservation is missing or no longer belongs to this handoff.");
        if (advances.Keys.Any(id => allocations.All(allocation => allocation.Kind != "ADVANCE" || allocation.Id != id)) ||
            schedules.Keys.Any(id => allocations.All(allocation => allocation.Kind != "SCHEDULE" || allocation.Id != id)))
            throw new InvalidOperationException("Stored payroll reservations do not exactly match the handoff settlement references.");
        var payrollLineIds = allocations.Select(allocation => allocation.PayrollLineId).Distinct().ToArray();
        var payrollLines = await db.PayrollRunLines.Include(line => line.Components)
            .Where(line => line.PayrollRunId == runId).ToDictionaryAsync(line => line.Id, cancellationToken);
        if (payrollLineIds.Any(id => !payrollLines.ContainsKey(id)))
            throw new InvalidOperationException("A payroll settlement reference points to a line outside its source payroll run.");
        foreach (var payrollLine in payrollLines.Values)
        {
            if (payrollLine.BranchId.HasValue && payrollLine.BranchId != handoff.BranchId)
                throw new InvalidOperationException("A payroll settlement line is outside the handoff branch.");
            var expectedDeduction = payrollLine.Components.Where(component => Normalize(component.ComponentType) == "DEDUCTION" &&
                    Normalize(component.ComponentCode) == "LOAN_DEDUCTION")
                .Sum(component => component.Amount);
            var allocatedDeduction = allocations.Where(allocation => allocation.PayrollLineId == payrollLine.Id)
                .Sum(allocation => allocation.Line.Amount);
            if (expectedDeduction < 0m || expectedDeduction != allocatedDeduction)
                throw new InvalidOperationException("Reserved loan and advance amounts do not match the employee payroll line's loan deduction component.");
        }
        foreach (var allocation in allocations)
        {
            var employeeCode = allocation.Kind == "ADVANCE"
                ? advances[allocation.Id].EmployeeCode
                : schedules[allocation.Id].EmployeeLoan?.EmployeeCode;
            if (employeeCode == null || !string.Equals(employeeCode, payrollLines[allocation.PayrollLineId].EmployeeCode, StringComparison.Ordinal))
                throw new InvalidOperationException("A payroll settlement source does not belong to the referenced employee line.");
        }
        var snapshots = new List<PayrollSettlementSnapshot>(allocations.Count);
        foreach (var allocation in allocations)
        {
            if (allocation.Kind == "ADVANCE")
            {
                var advance = advances[allocation.Id];
                if (advance.ReservedByHandoffId != handoff.Id || advance.Status != "ACTIVE" ||
                    !string.Equals(advance.TargetPayPeriod, handoff.PayPeriod, StringComparison.Ordinal) ||
                    advance.RemainingBalance < allocation.Line.Amount || Normalize(advance.CurrencyCode) != Normalize(allocation.Line.CurrencyCode))
                    throw new InvalidOperationException($"Reserved advance {advance.Id} changed before posting receipt.");
                snapshots.Add(new PayrollSettlementSnapshot("ADVANCE", advance.Id, allocation.PayrollLineId, allocation.Line.Amount,
                    advance.Status, null, null, null, null, null, advance.DeductedAmount, advance.RemainingBalance, null, null, null));
                advance.DeductedAmount += allocation.Line.Amount; advance.RemainingBalance -= allocation.Line.Amount;
                if (advance.RemainingBalance == 0) advance.Status = "RECOVERED";
                advance.ReservedByHandoffId = null; advance.UpdateUser = user; advance.UpdateDate = DateTime.UtcNow;
            }
            else
            {
                var schedule = schedules[allocation.Id]; var loan = schedule.EmployeeLoan!;
                var due = Math.Max(0m, schedule.ScheduledAmount - schedule.PaidAmount);
                if (schedule.ReservedByHandoffId != handoff.Id || loan.Status != "ACTIVE" ||
                    string.Compare(schedule.PayPeriod, handoff.PayPeriod, StringComparison.Ordinal) > 0 ||
                    due < allocation.Line.Amount || loan.RemainingBalance < allocation.Line.Amount ||
                    Normalize(loan.CurrencyCode) != Normalize(allocation.Line.CurrencyCode))
                    throw new InvalidOperationException($"Reserved loan schedule {schedule.Id} changed before posting receipt.");
                var appliedPaidDate = DateTime.UtcNow;
                snapshots.Add(new PayrollSettlementSnapshot("SCHEDULE", schedule.Id, allocation.PayrollLineId, allocation.Line.Amount,
                    schedule.Status, schedule.PaidAmount, schedule.CarriedForwardAmount, schedule.PaidDate, appliedPaidDate, schedule.PayrollRunLineId,
                    null, null, loan.TotalPaidAmount, loan.RemainingBalance, loan.Status));
                schedule.PaidAmount += allocation.Line.Amount; schedule.CarriedForwardAmount = due - allocation.Line.Amount;
                schedule.PayrollRunLineId = allocation.PayrollLineId; schedule.PaidDate = appliedPaidDate;
                schedule.Status = schedule.CarriedForwardAmount == 0 ? "PAID" : "PARTIAL"; schedule.ReservedByHandoffId = null;
                loan.TotalPaidAmount += allocation.Line.Amount; loan.RemainingBalance = Math.Max(0m, loan.RemainingBalance - allocation.Line.Amount);
                if (loan.RemainingBalance == 0) loan.Status = "COMPLETED";
                loan.UpdateUser = user; loan.UpdateDate = DateTime.UtcNow;
            }
        }
        return JsonSerializer.Serialize(snapshots);
    }

    private async Task RestorePayrollSettlementSnapshotAsync(IReadOnlyList<PayrollSettlementSnapshot> snapshots, string user, CancellationToken cancellationToken)
    {
        for (var index = snapshots.Count - 1; index >= 0; index--)
        {
            var snapshot = snapshots[index];
            if (snapshot.Kind == "ADVANCE")
            {
                if (!snapshot.PriorDeductedAmount.HasValue || !snapshot.PriorRemainingBalance.HasValue)
                    throw new InvalidOperationException("Advance settlement snapshot is incomplete.");
                var advance = await db.EmployeeAdvances.SingleOrDefaultAsync(a => a.Id == snapshot.SourceId, cancellationToken)
                    ?? throw new InvalidOperationException("A settled advance is missing; accounting reversal cannot safely restore HR.");
                var expectedRemaining = snapshot.PriorRemainingBalance.Value - snapshot.Amount;
                var expectedStatus = expectedRemaining == 0 ? "RECOVERED" : snapshot.PriorStatus;
                if (advance.ReservedByHandoffId != null || advance.DeductedAmount != snapshot.PriorDeductedAmount.Value + snapshot.Amount ||
                    advance.RemainingBalance != expectedRemaining || advance.Status != expectedStatus)
                    throw new InvalidOperationException("Advance has later or conflicting settlements; reverse those handoffs first.");
                advance.DeductedAmount = snapshot.PriorDeductedAmount.Value;
                advance.RemainingBalance = snapshot.PriorRemainingBalance.Value;
                advance.Status = snapshot.PriorStatus;
                advance.UpdateUser = user;
                advance.UpdateDate = DateTime.UtcNow;
                continue;
            }

            if (snapshot.Kind != "SCHEDULE" || !snapshot.PriorPaidAmount.HasValue || !snapshot.PriorCarriedForwardAmount.HasValue ||
                !snapshot.AppliedPaidDate.HasValue ||
                !snapshot.PriorLoanTotalPaidAmount.HasValue || !snapshot.PriorLoanRemainingBalance.HasValue || snapshot.PriorLoanStatus == null)
                throw new InvalidOperationException("Loan settlement snapshot is incomplete, predates the applied paid-date marker, or has an unknown source type; automatic reversal is blocked.");
            var schedule = await db.LoanRepaymentSchedules.Include(s => s.EmployeeLoan)
                .SingleOrDefaultAsync(s => s.Id == snapshot.SourceId, cancellationToken)
                ?? throw new InvalidOperationException("A settled loan installment is missing; accounting reversal cannot safely restore HR.");
            var loan = schedule.EmployeeLoan ?? throw new InvalidOperationException("A settled loan installment has no loan.");
            var expectedPaid = snapshot.PriorPaidAmount.Value + snapshot.Amount;
            var expectedCarried = Math.Max(0m, schedule.ScheduledAmount - snapshot.PriorPaidAmount.Value) - snapshot.Amount;
            var expectedScheduleStatus = expectedCarried == 0 ? "PAID" : "PARTIAL";
            var expectedLoanRemaining = Math.Max(0m, snapshot.PriorLoanRemainingBalance.Value - snapshot.Amount);
            var expectedLoanStatus = expectedLoanRemaining == 0 ? "COMPLETED" : snapshot.PriorLoanStatus;
            if (schedule.ReservedByHandoffId != null || schedule.PaidAmount != expectedPaid || schedule.CarriedForwardAmount != expectedCarried ||
                schedule.Status != expectedScheduleStatus || schedule.PaidDate != snapshot.AppliedPaidDate || schedule.PayrollRunLineId != snapshot.PayrollRunLineId ||
                loan.TotalPaidAmount != snapshot.PriorLoanTotalPaidAmount.Value + snapshot.Amount ||
                loan.RemainingBalance != expectedLoanRemaining || loan.Status != expectedLoanStatus)
                throw new InvalidOperationException("Loan or installment has later or conflicting settlements; reverse those handoffs first.");

            schedule.PaidAmount = snapshot.PriorPaidAmount.Value;
            schedule.CarriedForwardAmount = snapshot.PriorCarriedForwardAmount.Value;
            schedule.Status = snapshot.PriorStatus;
            schedule.PaidDate = snapshot.PriorPaidDate;
            schedule.PayrollRunLineId = snapshot.PriorSchedulePayrollLineId;
            loan.TotalPaidAmount = snapshot.PriorLoanTotalPaidAmount.Value;
            loan.RemainingBalance = snapshot.PriorLoanRemainingBalance.Value;
            loan.Status = snapshot.PriorLoanStatus;
            loan.UpdateUser = user;
            loan.UpdateDate = DateTime.UtcNow;
        }
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string ResolveHandoffCurrency(IEnumerable<HrAccountingHandoffLine> lines)
    {
        var currencies = lines.Select(line => Normalize(line.CurrencyCode)).Distinct(StringComparer.Ordinal).ToArray();
        if (currencies.Length != 1 || currencies[0].Length == 0)
            throw new InvalidOperationException("Accounting handoff requires one explicit currency across all lines.");
        return currencies[0];
    }

    private static void ValidatePayrollComponentBreakdown(ThinkOnErp.Domain.Entities.Hr.PayrollRunLine line)
    {
        var components = line.Components.ToArray();
        decimal Sum(string type, string code) => components
            .Where(component => Normalize(component.ComponentType) == type && Normalize(component.ComponentCode) == code)
            .Sum(component => component.Amount);

        var earnings = components.Where(component => Normalize(component.ComponentType) == "EARNING").Sum(component => component.Amount);
        var employeeSsc = Sum("DEDUCTION", "SSC_EMP");
        var employerSsc = Sum("EMPLOYER_CONTRIB", "SSC_EMPR");
        var incomeTax = Sum("DEDUCTION", "TAX_INCOME");
        var nationalContribution = Sum("DEDUCTION", "TAX_SOLIDARITY");
        var otherDeductions = components.Where(component => Normalize(component.ComponentType) == "DEDUCTION" &&
            Normalize(component.ComponentCode) is not ("UNPAID_LEAVE" or "SSC_EMP" or "TAX_INCOME" or "TAX_SOLIDARITY"))
            .Sum(component => component.Amount);

        if (earnings != line.GrossSalary || employeeSsc != line.SscEmployeeContrib || employerSsc != line.SscEmployerContrib ||
            incomeTax != line.IncomeTaxWithheld || nationalContribution != line.NationalContribWithheld ||
            otherDeductions != line.OtherDeductions ||
            line.TotalDeductions != employeeSsc + incomeTax + nationalContribution + otherDeductions ||
            line.NetPay != line.GrossSalary - line.TotalDeductions)
            throw new InvalidOperationException("Payroll line control totals do not match its immutable component breakdown.");
    }
}
