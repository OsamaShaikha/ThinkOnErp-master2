using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollService
{
    Task<PayrollCalculationResultDto> CreateAndCalculatePayrollRunAsync(CreatePayrollRunDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollRun> ApprovePayrollRunAsync(long payrollRunId, string user, CancellationToken cancellationToken = default);
    Task<PostGlVoucherResultDto> PostPayrollToGlAsync(long payrollRunId, string user, CancellationToken cancellationToken = default);
    Task<PayrollRun> ReversePayrollGlVoucherAsync(long payrollRunId, string user, CancellationToken cancellationToken = default);
    Task<PayrollCalculationResultDto?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? year = null, CancellationToken cancellationToken = default);
    Task<PayrollPeriod> CreatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default);
}

public sealed class PayrollService : IPayrollService
{
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IHrPayrollPostingService? _postingService;

    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPayrollCalculationEngine _calculationEngine;
    private readonly IPayrollValidationService _validationService;
    private readonly ILoanDeductionService _loanDeductionService;
    private readonly IPolicyRepository _policyRepo;
    private readonly IGlVoucherRepository _glVoucherRepo;

    public PayrollService(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        ILoanDeductionService loanDeductionService,
        IPolicyRepository policyRepo,
        IGlVoucherRepository glVoucherRepo, IHrUnitOfWork? unitOfWork = null, IHrPayrollPostingService? postingService = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _calculationEngine = calculationEngine;
        _validationService = validationService;
        _loanDeductionService = loanDeductionService;
        _policyRepo = policyRepo;
        _glVoucherRepo = glVoucherRepo;
        _unitOfWork = unitOfWork;
        _postingService = postingService;
    }

    public async Task<PayrollCalculationResultDto> CreateAndCalculatePayrollRunAsync(CreatePayrollRunDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (!dto.BranchId.HasValue) throw new ArgumentException("Calculate a separate payroll run for each branch.");
        // 1. Validation
        var validation = await _validationService.ValidatePreCalculationAsync(dto.CompanyId, dto.PayPeriod, dto.BranchId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Payroll validation failed: {string.Join(", ", validation.Errors)}");
        }

        var period = await _payrollPeriodRepo.GetPeriodByCodeAsync(dto.CompanyId, dto.PayPeriod, cancellationToken);
        if (period == null)
        {
            throw new InvalidOperationException($"Period '{dto.PayPeriod}' not found.");
        }

        var employees = await _payrollPeriodRepo.GetActiveEmployeesAsync(dto.BranchId, cancellationToken);

        // 2. Calculation
        var run = await _calculationEngine.CalculatePayrollForPeriodAsync(period, employees, dto.BranchId, user, cancellationToken);

        // 3. Persist run
        await _payrollPeriodRepo.AddPayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);

        return MapToResultDto(run);
    }

    public Task<PayrollRun> ApprovePayrollRunAsync(long payrollRunId, string user, CancellationToken cancellationToken = default) =>
        _unitOfWork == null ? ApprovePayrollRunAsyncCore(payrollRunId, user, cancellationToken) : _unitOfWork.ExecuteAsync(() => ApprovePayrollRunAsyncCore(payrollRunId, user, cancellationToken), cancellationToken);

    private async Task<PayrollRun> ApprovePayrollRunAsyncCore(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        var validation = await _validationService.ValidatePostApprovalAsync(payrollRunId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Payroll approval validation failed: {string.Join(", ", validation.Errors)}");
        }

        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null) throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");

        run.Status = "APPROVED";
        run.ApprovedBy = user;
        run.ApprovalDate = DateTime.UtcNow;
        run.UpdateUser = user;
        run.UpdateDate = DateTime.UtcNow;

        // Apply and persist loan deductions for each line
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(run, cancellationToken)
            ?? throw new InvalidOperationException("The payroll period and branch company must exist before approval.");
        var deductionPolicy = await _policyRepo.GetActiveDeductionPolicyAsync(period.CompanyId, period.StartDate, cancellationToken);
        foreach (var line in run.Lines)
        {
            decimal netBeforeLoans = line.GrossSalary - line.SscEmployeeContrib - line.IncomeTaxWithheld - line.NationalContribWithheld - (line.OtherDeductions - (line.Snapshot?.LoanDeductionsApplied ?? 0m));
            var settled = await _loanDeductionService.CalculateAndApplyDeductionsAsync(
                line.Id,
                line.EmployeeCode,
                run.PayPeriod,
                netBeforeLoans,
                deductionPolicy,
                persistChanges: true,
                cancellationToken: cancellationToken
            );
            if (line.Snapshot != null && settled.TotalActualDeduction != line.Snapshot.LoanDeductionsApplied)
                throw new InvalidOperationException("Loan balances changed since calculation. Recalculate the payroll before approval.");
        }

        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        return run;
    }

    public Task<PostGlVoucherResultDto> PostPayrollToGlAsync(long payrollRunId, string user, CancellationToken cancellationToken = default) =>
        _unitOfWork == null ? PostPayrollToGlAsyncCore(payrollRunId, user, cancellationToken) : _unitOfWork.ExecuteAsync(() => PostPayrollToGlAsyncCore(payrollRunId, user, cancellationToken), cancellationToken);

    private async Task<PostGlVoucherResultDto> PostPayrollToGlAsyncCore(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null) throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        if (run.Status != "APPROVED")
        {
            throw new InvalidOperationException($"Payroll run {payrollRunId} cannot be posted because its status is '{run.Status}' (must be APPROVED).");
        }

        long branchId = run.BranchId ?? throw new InvalidOperationException("The payroll run must have a branch.");
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(run, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        var postingDate = period.PayDate == default ? period.EndDate : period.PayDate;
        var posting = _postingService == null ? throw new InvalidOperationException("Payroll posting configuration service is required.") :
            await _postingService.ResolveAsync(branchId, postingDate, cancellationToken);
        int year = postingDate.Year;
        int month = postingDate.Month;
        int voucherType = posting.Configuration.VoucherType;

        long voucherNo = await _glVoucherRepo.GenerateNextSerialNoAsync(branchId, year, month, voucherType, "YEARLY", cancellationToken);

        decimal totalDebit = run.TotalGrossSalary + run.TotalEmployerSsc;
        decimal totalCredit = run.TotalNetSalary + run.TotalEmployeeSsc + run.TotalEmployerSsc + run.TotalIncomeTax + run.TotalNationalContrib + run.TotalOtherDeductions;

        if (Math.Abs(totalDebit - totalCredit) > 0.001m) throw new InvalidOperationException("Payroll journal is not balanced.");

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

    public Task<PayrollRun> ReversePayrollGlVoucherAsync(long payrollRunId, string user, CancellationToken cancellationToken = default) =>
        _unitOfWork == null ? ReversePayrollGlVoucherAsyncCore(payrollRunId, user, cancellationToken) : _unitOfWork.ExecuteAsync(() => ReversePayrollGlVoucherAsyncCore(payrollRunId, user, cancellationToken), cancellationToken);

    private async Task<PayrollRun> ReversePayrollGlVoucherAsyncCore(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null) throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        if (run.Status != "POSTED_TO_GL" || !run.JournalVoucherId.HasValue)
        {
            throw new InvalidOperationException($"Payroll run {payrollRunId} is not in POSTED_TO_GL status.");
        }

        var voucher = await _glVoucherRepo.GetByIdAsync(run.JournalVoucherId.Value, cancellationToken);
        if (voucher != null)
        {
            voucher.IsReversed = true;
            voucher.ReverseUser = user;
            voucher.ReverseDate = DateTime.UtcNow;
            voucher.Status = 4; // Reversed
            await _glVoucherRepo.SaveChangesAsync(cancellationToken);
        }

        run.Status = "APPROVED";
        run.JournalVoucherId = null;
        run.UpdateUser = user;
        run.UpdateDate = DateTime.UtcNow;

        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);

        return run;
    }

    public async Task<PayrollCalculationResultDto?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(id, cancellationToken);
        return run == null ? null : MapToResultDto(run);
    }

    public async Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? year = null, CancellationToken cancellationToken = default)
    {
        return await _payrollPeriodRepo.GetPeriodsAsync(companyId, year, cancellationToken);
    }

    public async Task<PayrollPeriod> CreatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
    {
        await _payrollPeriodRepo.AddPeriodAsync(period, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        return period;
    }

    private static PayrollCalculationResultDto MapToResultDto(PayrollRun run)
    {
        var payslips = run.Lines.Select(l => new PayslipSummaryDto(
            l.Id,
            l.EmployeeCode,
            l.Employee?.NameLocal ?? l.EmployeeCode,
            l.BasicSalary,
            l.GrossSalary,
            l.TotalEarnings,
            l.SscEmployeeContrib,
            l.IncomeTaxWithheld,
            l.OtherDeductions,
            l.TotalDeductions,
            l.NetPay,
            l.PaymentMethod
        )).ToList();

        return new PayrollCalculationResultDto(
            run.Id,
            run.PayPeriod,
            run.Lines.Count,
            run.TotalGrossSalary,
            run.TotalNetSalary,
            run.TotalEmployeeSsc,
            run.TotalEmployerSsc,
            run.TotalIncomeTax,
            run.TotalNationalContrib,
            run.TotalOtherDeductions,
            payslips
        );
    }
}
