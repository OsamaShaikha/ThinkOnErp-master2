using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Accounting.Legacy;

public sealed partial class LegacyPayrollJournalService
{
    private async Task<T> WithFinancialLockAsync<T>(long id, Func<Task<T>> action, CancellationToken ct)
    {
        if (_unitOfWork == null) return await action();
        var snapshot = await _payrollPeriodRepo.GetPayrollRunSnapshotAsync(id, ct)
            ?? throw new KeyNotFoundException("Payroll run was not found.");
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(snapshot, ct)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        return await _unitOfWork.ExecutePayrollFinancialAsync(period.CompanyId, period.PeriodCode, action, ct);
    }

    private static void ValidateOriginal(GlVoucherHeader voucher, PayrollRun run)
    {
        if (voucher.BranchId != run.BranchId || voucher.SourceSystemCode != "HR_PAYROLL" || voucher.SourceRefId != run.Id || voucher.Status != 3)
            throw new InvalidOperationException("The linked payroll journal does not match the run or is not a posted journal. Review its accounting history.");
        ValidateJournal(voucher);
    }

    private static void ValidateJournal(GlVoucherHeader voucher)
    {
        if (voucher.IsStandby || voucher.Details.Count == 0 || voucher.Details.Select(d => d.LineSer).Distinct().Count() != voucher.Details.Count ||
            voucher.Details.Any(d => d.BranchId != voucher.BranchId || d.CurrencyId <= 0 || d.ExchangeRate <= 0 || d.IsSettlement) ||
            voucher.TotalAmount != voucher.Details.Sum(d => d.Debit) ||
            voucher.TotalLocalDebit != voucher.Details.Sum(d => d.LocalDebit) ||
            voucher.TotalLocalCredit != voucher.Details.Sum(d => d.LocalCredit) ||
            voucher.TotalLocalDebit != voucher.TotalLocalCredit ||
            voucher.Details.Sum(d => d.BaseDebit) != voucher.Details.Sum(d => d.BaseCredit) ||
            voucher.Details.GroupBy(d => d.CurrencyId).Any(g => g.Sum(d => d.Debit) != g.Sum(d => d.Credit)))
            throw new InvalidOperationException("The payroll journal has incomplete, unbalanced or unsupported settlement details. Review it before posting or reversal.");
    }

    private async Task<GlVoucherHeader> ReversalProofAsync(GlVoucherHeader original, CancellationToken ct)
    {
        var reversals = await _glVoucherRepo.GetBySourceAsync(original.BranchId, "HR_PAYROLL_REVERSAL", original.Id, ct);
        if (reversals.Count != 1 || !original.IsReversed)
            throw new InvalidOperationException("The payroll journal has no unique, linked financial reversal. Review its accounting history.");
        var reversal = reversals[0];
        ValidateJournal(reversal);
        if (reversal.Status != 3 || reversal.IsReversed || reversal.BranchId != original.BranchId ||
            reversal.SourceSystemCode != "HR_PAYROLL_REVERSAL" || reversal.SourceRefId != original.Id ||
            reversal.VoucherType != original.VoucherType || reversal.Details.Count != original.Details.Count ||
            reversal.TotalAmount != original.Details.Sum(d => d.Credit) ||
            !original.Details.All(d => reversal.Details.Any(r => IsInverse(d, r))))
            throw new InvalidOperationException("The linked reversal does not exactly offset the payroll journal.");
        return reversal;
    }

    private static bool IsInverse(GlVoucherDetail original, GlVoucherDetail reversal) =>
        original.LineSer == reversal.LineSer && original.AccountCode == reversal.AccountCode &&
        original.Debit == reversal.Credit && original.Credit == reversal.Debit &&
        original.LocalDebit == reversal.LocalCredit && original.LocalCredit == reversal.LocalDebit &&
        original.BaseDebit == reversal.BaseCredit && original.BaseCredit == reversal.BaseDebit &&
        original.CurrencyId == reversal.CurrencyId && original.ExchangeRate == reversal.ExchangeRate &&
        original.BranchId == reversal.BranchId && original.CostCenterCode == reversal.CostCenterCode &&
        original.CostCenterMgrCode == reversal.CostCenterMgrCode && original.CostCenterMnrCode == reversal.CostCenterMnrCode &&
        original.PartyType == reversal.PartyType && original.PartyCode == reversal.PartyCode && original.IsSettlement == reversal.IsSettlement;

    private async Task<GlVoucherHeader?> ValidatePostingHistoryAsync(PayrollRun run, CancellationToken ct)
    {
        var branch = run.BranchId ?? throw new InvalidOperationException("The payroll run must have a branch.");
        var journals = await _glVoucherRepo.GetBySourceAsync(branch, "HR_PAYROLL", run.Id, ct);
        if (journals.Any(v => v.Status != 3))
            throw new InvalidOperationException("Payroll source history contains a non-posted or legacy journal. Review it before continuing.");
        var active = journals.Where(v => v.Status == 3 && !v.IsReversed).ToArray();
        if (run.Status == "POSTED_TO_GL")
        {
            if (active.Length != 1 || active[0].Id != run.JournalVoucherId)
                throw new InvalidOperationException("The payroll has no unique active journal matching its link.");
            ValidateOriginal(active[0], run);
            return active[0];
        }
        if (active.Length != 0)
            throw new InvalidOperationException("An active journal already exists for this payroll. Review the existing link before posting.");
        if (run.JournalVoucherId.HasValue)
        {
            var original = journals.SingleOrDefault(v => v.Id == run.JournalVoucherId)
                ?? throw new InvalidOperationException("The original payroll journal link is missing or invalid.");
            ValidateOriginal(original, run);
            await ReversalProofAsync(original, ct);
        }
        else if (journals.Count != 0)
            throw new InvalidOperationException("Payroll journal history exists without a preserved link. Review the legacy history before reposting.");
        return null;
    }

    private static PostGlVoucherResultDto PostingResult(PayrollRun run, GlVoucherHeader voucher) =>
        new(run.Id, voucher.Id, voucher.Details.Sum(d => d.Debit), voucher.Details.Sum(d => d.Credit), true, voucher.PostDate ?? voucher.VoucherDate);

    public Task<PayrollRun> ReversePayrollGlVoucherAsync(long payrollRunId, ReversePayrollGlDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.ReversalDate == default || string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 500)
            throw new ArgumentException("An explicit reversal date and a reason of at most 500 characters are required.");
        var request = dto with { ReversalDate = dto.ReversalDate.Date, Reason = dto.Reason.Trim() };
        return WithFinancialLockAsync(payrollRunId, () => ReversePayrollGlCoreAsync(payrollRunId, request, user, cancellationToken), cancellationToken);
    }

    private async Task<PayrollRun> ReversePayrollGlCoreAsync(long id, ReversePayrollGlDto dto, string user, CancellationToken ct)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(id, ct) ?? throw new KeyNotFoundException("Payroll run was not found.");
        if (run.Status is not ("POSTED_TO_GL" or "APPROVED") || !run.JournalVoucherId.HasValue)
            throw new InvalidOperationException("A posted payroll journal link is required for reversal.");
        var original = await _glVoucherRepo.GetByIdAsync(run.JournalVoucherId.Value, ct)
            ?? throw new InvalidOperationException("The linked payroll journal was not found.");
        ValidateOriginal(original, run);
        var description = "Payroll GL reversal: " + dto.Reason;
        if (run.Status == "APPROVED" && original.IsReversed)
        {
            var proof = await ReversalProofAsync(original, ct);
            if (proof.VoucherDate.Date != dto.ReversalDate || proof.Description != description)
                throw new InvalidOperationException("This journal was already reversed using a different date or reason.");
            return run;
        }
        if (run.Status != "POSTED_TO_GL" || original.IsReversed)
            throw new InvalidOperationException("The payroll state and original journal reversal state do not agree.");
        var history = await _glVoucherRepo.GetBySourceAsync(original.BranchId, "HR_PAYROLL", run.Id, ct);
        if (history.Any(v => v.Status != 3) || history.Count(v => v.Status == 3 && !v.IsReversed) != 1 || !history.Any(v => v.Id == original.Id))
            throw new InvalidOperationException("Resolve duplicate or missing payroll journal history before reversal.");
        if ((await _glVoucherRepo.GetBySourceAsync(original.BranchId, "HR_PAYROLL_REVERSAL", original.Id, ct)).Count != 0)
            throw new InvalidOperationException("A financial reversal already exists. Review the payroll link before retrying.");
        if (dto.ReversalDate < original.VoucherDate.Date)
            throw new ArgumentException("The reversal date cannot precede the original journal date.");
        var context = _postingService == null ? throw new InvalidOperationException("Payroll posting configuration service is required.") :
            await _postingService.ResolveReversalAsync(original.BranchId, dto.ReversalDate, original.VoucherType, ct);
        var number = await _glVoucherRepo.GenerateNextSerialNoAsync(original.BranchId, dto.ReversalDate.Year,
            dto.ReversalDate.Month, original.VoucherType, context.SerialResetPolicy, ct);
        var now = DateTime.UtcNow;
        var reversal = new GlVoucherHeader
        {
            BranchId = original.BranchId, FiscalYearId = context.FiscalYearId, VoucherYear = dto.ReversalDate.Year,
            VoucherMonth = dto.ReversalDate.Month, VoucherType = original.VoucherType, VoucherNo = number, VoucherDate = dto.ReversalDate,
            Description = description, TotalAmount = original.Details.Sum(d => d.Credit),
            TotalLocalDebit = original.TotalLocalCredit, TotalLocalCredit = original.TotalLocalDebit,
            Status = 3, IsAutoRecord = true, IsReviewed = true, ReviewUser = user, ReviewDate = now,
            SourceSystemCode = "HR_PAYROLL_REVERSAL", SourceRefId = original.Id, PostUser = user, PostDate = now, CreationUser = user, CreationDate = now
        };
        foreach (var detail in original.Details)
            reversal.Details.Add(new GlVoucherDetail
            {
                LineSer = detail.LineSer, AccountCode = detail.AccountCode, Debit = detail.Credit, Credit = detail.Debit,
                LocalDebit = detail.LocalCredit, LocalCredit = detail.LocalDebit, BaseDebit = detail.BaseCredit, BaseCredit = detail.BaseDebit,
                CurrencyId = detail.CurrencyId, ExchangeRate = detail.ExchangeRate, BranchId = detail.BranchId,
                CostCenterCode = detail.CostCenterCode, CostCenterMgrCode = detail.CostCenterMgrCode, CostCenterMnrCode = detail.CostCenterMnrCode,
                PartyType = detail.PartyType, PartyCode = detail.PartyCode, IsSettlement = detail.IsSettlement, Description = detail.Description
            });
        ValidateJournal(reversal);
        await _glVoucherRepo.AddVoucherAsync(reversal, ct);
        original.IsReversed = true; original.ReverseUser = user; original.ReverseDate = now; original.UpdateUser = user; original.UpdateDate = now;
        // Keep the original posted amounts and status: the inverse journal offsets them.
        await _glVoucherRepo.SaveChangesAsync(ct);
        run.Status = "APPROVED"; // payroll approval/loan settlements are unchanged (PAY-09).
        // JournalVoucherId continues pointing to the original; reversal links back via SourceRefId.
        run.UpdateUser = user; run.UpdateDate = now;
        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, ct);
        await _payrollPeriodRepo.SaveChangesAsync(ct);
        return run;
    }
}
