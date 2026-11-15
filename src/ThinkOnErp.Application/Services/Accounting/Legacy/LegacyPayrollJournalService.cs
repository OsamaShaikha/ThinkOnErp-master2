using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Accounting.Legacy;

/// <summary>
/// Unregistered legacy journal engine retained for migration/regression only.
/// Production Accounting must consume an accepted immutable handoff (INT-06),
/// not invoke this live-HR-run adapter. No HTTP endpoint resolves this class.
/// </summary>
public sealed partial class LegacyPayrollJournalService
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IGlVoucherRepository _glVoucherRepo;
    private readonly IHrPayrollPostingService? _postingService;
    private readonly IHrUnitOfWork? _unitOfWork;
    public LegacyPayrollJournalService(IPayrollPeriodRepository repository, IGlVoucherRepository journals,
        IHrPayrollPostingService? postingService, IHrUnitOfWork? unitOfWork = null)
    { _payrollPeriodRepo = repository; _glVoucherRepo = journals; _postingService = postingService; _unitOfWork = unitOfWork; }

    public Task<PostGlVoucherResultDto> PostPayrollToGlAsync(long payrollRunId, string user, CancellationToken cancellationToken = default) =>
        WithFinancialLockAsync(payrollRunId, () => PostPayrollToGlAsyncCore(payrollRunId, user, cancellationToken), cancellationToken);

    private async Task<PostGlVoucherResultDto> PostPayrollToGlAsyncCore(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null) throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        var previous = await ValidatePostingHistoryAsync(run, cancellationToken);
        if (previous != null) return PostingResult(run, previous);
        if (run.Status != "APPROVED")
        {
            throw new InvalidOperationException($"Payroll run {payrollRunId} cannot be posted because its status is '{run.Status}' (must be APPROVED).");
        }

        long branchId = run.BranchId ?? throw new InvalidOperationException("The payroll run must have a branch.");
        if (run.Lines.Any(l => l.BranchId != branchId))
            throw new InvalidOperationException("Every payroll line must belong to the run branch before posting.");
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(run, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        var postingDate = period.PayDate == default ? period.EndDate : period.PayDate;
        var posting = _postingService == null ? throw new InvalidOperationException("Payroll posting configuration service is required.") :
            await _postingService.ResolveAsync(branchId, postingDate, cancellationToken);
        var runCurrency = PayrollCurrencyRules.RequireSingleCurrency(run.Lines.Select(l => l.CurrencyCode));
        var postingCurrency = SalaryPaymentMetadata.Normalize(posting.CurrencyCode);
        if (runCurrency == null || postingCurrency.Length == 0 || runCurrency != postingCurrency)
            throw new InvalidOperationException("Payroll line currency must match the configured posting currency. Foreign-currency conversion is not configured.");
        PayrollPostingRules.ValidateTotals(run);
        int year = postingDate.Year;
        int month = postingDate.Month;
        int voucherType = posting.Configuration.VoucherType;

        long voucherNo = await _glVoucherRepo.GenerateNextSerialNoAsync(branchId, year, month, voucherType, posting.SerialResetPolicy, cancellationToken);

        decimal totalDebit = run.TotalGrossSalary + run.TotalEmployerSsc;
        decimal totalCredit = run.TotalNetSalary + run.TotalEmployeeSsc + run.TotalEmployerSsc + run.TotalIncomeTax + run.TotalNationalContrib + run.TotalOtherDeductions;

        var voucher = new GlVoucherHeader
        {
            BranchId = branchId,
            FiscalYearId = posting.FiscalYearId,
            VoucherYear = year,
            VoucherMonth = month,
            VoucherType = voucherType,
            VoucherNo = voucherNo,
            VoucherDate = postingDate,
            Description = $"قيد استحقاق رواتب وأجور شهر {run.PayPeriod}",
            TotalAmount = totalDebit,
            TotalLocalDebit = totalDebit,
            TotalLocalCredit = totalCredit,
            Status = 3, // Posted
            IsAutoRecord = true,
            SourceSystemCode = "HR_PAYROLL",
            SourceRefId = run.Id,
            IsReviewed = true,
            ReviewUser = user,
            ReviewDate = DateTime.UtcNow,
            PostUser = user,
            PostDate = DateTime.UtcNow,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        int lineSer = 1;

        // Line 1: Debit Gross Salaries Expense
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = posting.Configuration.SalaryExpense, // مصاريف الرواتب والأجور
            Debit = run.TotalGrossSalary,
            Credit = 0m,
            LocalDebit = run.TotalGrossSalary,
            LocalCredit = 0m,
            BaseDebit = run.TotalGrossSalary,
            BaseCredit = 0m,
            Description = $"مصاريف الرواتب والأجور - {run.PayPeriod}",
            CurrencyId = posting.Configuration.CurrencyId,
            ExchangeRate = 1.0m,
            BranchId = branchId
        });

        // Line 2: Debit SSC Employer Contribution Expense
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = posting.Configuration.EmployerSscExpense, // مصاريف مساهمة الشركة في الضمان
            Debit = run.TotalEmployerSsc,
            Credit = 0m,
            LocalDebit = run.TotalEmployerSsc,
            LocalCredit = 0m,
            BaseDebit = run.TotalEmployerSsc,
            BaseCredit = 0m,
            Description = $"مساهمة الشركة في الضمان الاجتماعي - {run.PayPeriod}",
            CurrencyId = posting.Configuration.CurrencyId,
            ExchangeRate = 1.0m,
            BranchId = branchId
        });

        // Line 3: Credit Net Salaries Payable
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = posting.Configuration.SalaryPayable, // أمانات رواتب مستحقة الدفع
            Debit = 0m,
            Credit = run.TotalNetSalary,
            LocalDebit = 0m,
            LocalCredit = run.TotalNetSalary,
            BaseDebit = 0m,
            BaseCredit = run.TotalNetSalary,
            Description = $"صافي الرواتب المستحقة للموظفين - {run.PayPeriod}",
            CurrencyId = posting.Configuration.CurrencyId,
            ExchangeRate = 1.0m,
            BranchId = branchId
        });

        // Line 4: Credit SSC Payable (Employee + Employer)
        decimal totalSscPayable = run.TotalEmployeeSsc + run.TotalEmployerSsc;
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = posting.Configuration.SscPayable, // أمانات المؤسسة العامة للضمان الاجتماعي
            Debit = 0m,
            Credit = totalSscPayable,
            LocalDebit = 0m,
            LocalCredit = totalSscPayable,
            BaseDebit = 0m,
            BaseCredit = totalSscPayable,
            Description = $"أمانات الضمان الاجتماعي (موظف + صاحب عمل) - {run.PayPeriod}",
            CurrencyId = posting.Configuration.CurrencyId,
            ExchangeRate = 1.0m,
            BranchId = branchId
        });

        // Line 5: Credit Income Tax & Solidarity Withholding Payable
        decimal totalTaxPayable = run.TotalIncomeTax + run.TotalNationalContrib;
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = posting.Configuration.TaxPayable, // أمانات ضريبة الدخل المستقطعة
            Debit = 0m,
            Credit = totalTaxPayable,
            LocalDebit = 0m,
            LocalCredit = totalTaxPayable,
            BaseDebit = 0m,
            BaseCredit = totalTaxPayable,
            Description = $"أمانات ضريبة الدخل والتكافل الوطني - {run.PayPeriod}",
            CurrencyId = posting.Configuration.CurrencyId,
            ExchangeRate = 1.0m,
            BranchId = branchId
        });

        // Line 6: Credit Other Deductions / Advances Recoverable
        if (run.TotalOtherDeductions > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = posting.Configuration.DeductionsPayable, // سلف وقروض الموظفين / اقتطاعات أخرى
                Debit = 0m,
                Credit = run.TotalOtherDeductions,
                LocalDebit = 0m,
                LocalCredit = run.TotalOtherDeductions,
                BaseDebit = 0m,
                BaseCredit = run.TotalOtherDeductions,
                Description = $"استرداد سلف واقتطاعات موظفين - {run.PayPeriod}",
                CurrencyId = posting.Configuration.CurrencyId,
                ExchangeRate = 1.0m,
                BranchId = branchId
            });
        }

        await _glVoucherRepo.AddVoucherAsync(voucher, cancellationToken);
        await _glVoucherRepo.SaveChangesAsync(cancellationToken);

        run.Status = "POSTED_TO_GL";
        run.JournalVoucherId = voucher.Id;
        run.PostedBy = user;
        run.PostDate = DateTime.UtcNow;
        run.UpdateUser = user;
        run.UpdateDate = DateTime.UtcNow;

        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);

        bool isBalanced = Math.Abs(totalDebit - totalCredit) < 0.001m;

        return new PostGlVoucherResultDto(
            run.Id,
            voucher.Id,
            totalDebit,
            totalCredit,
            isBalanced,
            DateTime.UtcNow
        );
    }

}
