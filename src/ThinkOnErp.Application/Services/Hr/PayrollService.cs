using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class PayrollService : IPayrollService
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPayrollCalculationEngine _calculationEngine;
    private readonly IPayrollValidationService _validationService;
    private readonly IGlVoucherRepository _glVoucherRepository;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(
        IPayrollRepository payrollRepository,
        IEmployeeRepository employeeRepository,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        IGlVoucherRepository glVoucherRepository,
        ILogger<PayrollService> logger)
    {
        _payrollRepository = payrollRepository ?? throw new ArgumentNullException(nameof(payrollRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _calculationEngine = calculationEngine ?? throw new ArgumentNullException(nameof(calculationEngine));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _glVoucherRepository = glVoucherRepository ?? throw new ArgumentNullException(nameof(glVoucherRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<PayrollRunDto>> GetAllRunsAsync(string? payPeriod = null, long? branchId = null, string? status = null)
    {
        var runs = await _payrollRepository.GetAllRunsAsync(payPeriod, branchId, status);
        return runs.Select(MapToRunDto).ToList();
    }

    public async Task<PayrollRunDto?> GetRunByIdAsync(long id)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        return run == null ? null : MapToRunDto(run);
    }

    public async Task<PayrollRunDto> CreateRunAsync(CreatePayrollRunDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var period = dto.PayPeriod.Trim();

        var existing = await _payrollRepository.GetRunByPeriodAndBranchAsync(period, dto.BranchId);
        if (existing != null && existing.Status != "DRAFT")
        {
            throw new HrConflictException($"مسير رواتب لشهر ({period}) مسجل مسبقاً بحالة ({existing.Status}).", "PAYROLL_RUN_EXISTS");
        }

        var run = new PayrollRun
        {
            PayPeriod = period,
            BranchId = dto.BranchId,
            RunDate = DateTime.UtcNow,
            Status = "DRAFT",
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _payrollRepository.AddRunAsync(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Created payroll run #{Id} for period {Period} by {User}", run.Id, period, currentUser);
        return MapToRunDto(run);
    }

    public async Task<PayrollRunDto> CalculateRunAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "DRAFT" && run.Status != "CALCULATED" && run.Status != "UNDER_REVIEW")
        {
            throw new HrValidationException($"لا يمكن إعادة احتساب مسير رواتب بحالة ({run.Status}). المسير في حالة مقفلة أو معتمدة.", "INVALID_RUN_STATE_FOR_CALCULATION");
        }

        // Clean out existing lines if recalculating
        if (run.Lines.Count > 0)
        {
            _payrollRepository.RemoveRunLines(run.Lines.ToList());
            run.Lines.Clear();
        }

        var employees = await _employeeRepository.GetAllAsync(branchId: run.BranchId, activeOnly: true);
        var eligibleEmployees = employees.Where(e => e.EmploymentStatus != "TERMINATED").ToList();

        if (eligibleEmployees.Count == 0)
        {
            throw new HrValidationException("لا يوجد موظفون مؤهلون للاحتساب في هذا الفرع.", "NO_ELIGIBLE_EMPLOYEES");
        }

        foreach (var emp in eligibleEmployees)
        {
            try
            {
                var line = await _calculationEngine.CalculateEmployeePayrollAsync(
                    employeeCode: emp.EmployeeCode,
                    payPeriod: run.PayPeriod,
                    payrollRunId: run.Id,
                    currentUser: currentUser);

                run.Lines.Add(line);
            }
            catch (HrValidationException ex) when (ex.ErrorCode == "NO_ACTIVE_SALARY_STRUCTURE")
            {
                _logger.LogWarning("Skipped employee {Emp} from payroll run #{Id} due to missing active salary structure", emp.EmployeeCode, id);
            }
        }

        if (run.Lines.Count == 0)
        {
            throw new HrValidationException("تعذر احتساب أي موظف في هذا المسير. يرجى التأكد من وجود هياكل رواتب فعالة.", "EMPTY_CALCULATION");
        }

        // Aggregate totals
        run.TotalGrossSalary = run.Lines.Sum(l => l.GrossSalary);
        run.TotalEmployeeSsc = run.Lines.Sum(l => l.SscEmployeeContribution);
        run.TotalEmployerSsc = run.Lines.Sum(l => l.SscEmployerContribution);
        run.TotalIncomeTax = run.Lines.Sum(l => l.IncomeTaxWithheld);
        run.TotalNationalContribution = run.Lines.Sum(l => l.NationalContributionWithheld);
        run.TotalOtherDeductions = run.Lines.Sum(l => l.OtherDeductions);
        run.TotalNetSalary = run.Lines.Sum(l => l.NetPay);
        run.Status = "CALCULATED";
        run.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        run.UpdateDate = DateTime.UtcNow;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Calculated payroll run #{Id} with {Count} employees, Total Gross {Gross}, Total Net {Net}", id, run.Lines.Count, run.TotalGrossSalary, run.TotalNetSalary);

        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollValidationResultDto> ValidateRunAsync(long id)
    {
        return await _validationService.ValidatePayrollRunAsync(id);
    }

    public async Task<PayrollRunDto> SubmitForApprovalAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "CALCULATED")
        {
            throw new HrValidationException($"لا يمكن تقديم مسير رواتب للاعتماد بحالة ({run.Status}). يجب احتسابه أولاً.", "INVALID_STATUS");
        }

        // Pre-submission validation
        var validation = await _validationService.ValidatePayrollRunAsync(id);
        if (!validation.IsValid)
        {
            throw new HrValidationException($"لا يمكن تقديم المسير للاعتماد لوجود أخطاء تدقيق ({validation.ErrorCount} خطأ).", "VALIDATION_FAILED");
        }

        run.Status = "UNDER_REVIEW";
        run.UpdateUser = currentUser;
        run.UpdateDate = DateTime.UtcNow;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollRunDto> ApproveRunAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "UNDER_REVIEW" && run.Status != "CALCULATED")
        {
            throw new HrValidationException($"لا يمكن اعتماد مسير رواتب بحالة ({run.Status}).", "INVALID_STATUS");
        }

        run.Status = "APPROVED";
        run.ApprovedBy = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        run.ApprovalDate = DateTime.UtcNow;
        run.UpdateUser = run.ApprovedBy;
        run.UpdateDate = DateTime.UtcNow;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Approved payroll run #{Id} by {User}", id, currentUser);
        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollRunDto> PostRunToGlAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "APPROVED")
        {
            throw new HrValidationException($"يجب اعتماد مسير الرواتب قبل ترحيله للحسابات العامة. الحالة الحالية: ({run.Status}).", "RUN_NOT_APPROVED");
        }

        if (run.JournalVoucherId.HasValue)
        {
            throw new HrConflictException($"تم ترحيل مسير الرواتب مسبقاً بسند قيد رقم ({run.JournalVoucherId}).", "ALREADY_POSTED_TO_GL");
        }

        var branchId = run.BranchId ?? 1;
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        var nextVoucherNo = await _glVoucherRepository.GenerateNextSerialNoAsync(branchId, year, month, 1, "YEARLY");

        // Construct Balanced Double-Entry GL Journal Voucher
        var voucher = new GlVoucherHeader
        {
            BranchId = branchId,
            FiscalYearId = year,
            VoucherYear = year,
            VoucherMonth = month,
            VoucherType = 1, // Standard JV
            VoucherNo = nextVoucherNo,
            VoucherDate = now,
            Description = $"Payroll Run GL Posting for period {run.PayPeriod} (Run #{run.Id})",
            TotalAmount = run.TotalGrossSalary + run.TotalEmployerSsc,
            TotalLocalDebit = run.TotalGrossSalary + run.TotalEmployerSsc,
            TotalLocalCredit = run.TotalGrossSalary + run.TotalEmployerSsc,
            Status = 3, // Posted
            IsAutoRecord = true,
            SourceSystemCode = "HR_PAYROLL",
            SourceRefId = run.Id,
            CreationUser = currentUser,
            CreationDate = now,
            PostUser = currentUser,
            PostDate = now
        };

        var lineSer = 1;

        // Debit: Salaries & Wages Expense (grouped by Cost Center)
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "621101",
            Debit = run.TotalGrossSalary,
            LocalDebit = run.TotalGrossSalary,
            BaseDebit = run.TotalGrossSalary,
            Description = $"Salaries & Wages for {run.PayPeriod}"
        });

        // Debit: Employer Social Security Expense
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "621102",
            Debit = run.TotalEmployerSsc,
            LocalDebit = run.TotalEmployerSsc,
            BaseDebit = run.TotalEmployerSsc,
            Description = $"Employer Social Security Contribution for {run.PayPeriod}"
        });

        // Credit: Salaries Payable (Net Pay)
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "213101",
            Credit = run.TotalNetSalary,
            LocalCredit = run.TotalNetSalary,
            BaseCredit = run.TotalNetSalary,
            Description = $"Net Salaries Payable for {run.PayPeriod}"
        });

        // Credit: Social Security Payable (Employee + Employer)
        var totalSsc = run.TotalEmployeeSsc + run.TotalEmployerSsc;
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "213201",
            Credit = totalSsc,
            LocalCredit = totalSsc,
            BaseCredit = totalSsc,
            Description = $"Social Security Withholdings & Contributions for {run.PayPeriod}"
        });

        // Credit: Income Tax Payable (Income tax + National surcharge)
        var totalTax = run.TotalIncomeTax + run.TotalNationalContribution;
        if (totalTax > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "214203",
                Credit = totalTax,
                LocalCredit = totalTax,
                BaseCredit = totalTax,
                Description = $"Employee Income Tax Withheld for {run.PayPeriod}"
            });
        }

        // Credit: Other Employee Deductions / Loans / Advances
        if (run.TotalOtherDeductions > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "213202",
                Credit = run.TotalOtherDeductions,
                LocalCredit = run.TotalOtherDeductions,
                BaseCredit = run.TotalOtherDeductions,
                Description = $"Other Deductions & Loans for {run.PayPeriod}"
            });
        }

        await _glVoucherRepository.AddVoucherAsync(voucher);
        await _glVoucherRepository.SaveChangesAsync();

        run.Status = "POSTED";
        run.JournalVoucherId = voucher.Id;
        run.PostedBy = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        run.PostDate = now;
        run.UpdateUser = run.PostedBy;
        run.UpdateDate = now;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Posted payroll run #{Id} to GL with Journal Voucher #{VoucherId}", id, voucher.Id);
        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollRunDto> LockRunAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: false);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "POSTED" && run.Status != "PAID")
        {
            throw new HrValidationException($"لا يمكن قفل مسير رواتب غير مرحل أو غير مدفوع. الحالة الحالية: ({run.Status}).", "INVALID_STATUS");
        }

        run.Status = "LOCKED";
        run.UpdateUser = currentUser;
        run.UpdateDate = DateTime.UtcNow;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Locked payroll run #{Id} by {User}", id, currentUser);
        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollRunDto> ReverseRunAsync(long id, string reason, string currentUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "POSTED")
        {
            throw new HrValidationException($"لا يمكن عكس مسير رواتب إلا إذا كان مرحلاً (POSTED). الحالة الحالية: ({run.Status}).", "INVALID_STATUS_FOR_REVERSAL");
        }

        var branchId = run.BranchId ?? 1;
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        var nextVoucherNo = await _glVoucherRepository.GenerateNextSerialNoAsync(branchId, year, month, 1, "YEARLY");

        // Reversal Voucher
        var revVoucher = new GlVoucherHeader
        {
            BranchId = branchId,
            FiscalYearId = year,
            VoucherYear = year,
            VoucherMonth = month,
            VoucherType = 1,
            VoucherNo = nextVoucherNo,
            VoucherDate = now,
            Description = $"Reversal of Payroll Run #{run.Id} for {run.PayPeriod}. Reason: {reason}",
            TotalAmount = run.TotalGrossSalary + run.TotalEmployerSsc,
            TotalLocalDebit = run.TotalGrossSalary + run.TotalEmployerSsc,
            TotalLocalCredit = run.TotalGrossSalary + run.TotalEmployerSsc,
            Status = 3,
            IsAutoRecord = true,
            SourceSystemCode = "HR_PAYROLL_REVERSAL",
            SourceRefId = run.Id,
            CreationUser = currentUser,
            CreationDate = now,
            PostUser = currentUser,
            PostDate = now
        };

        var lineSer = 1;
        // Invert credits and debits
        revVoucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "621101",
            Credit = run.TotalGrossSalary,
            LocalCredit = run.TotalGrossSalary,
            BaseCredit = run.TotalGrossSalary,
            Description = $"Reversal: Salaries & Wages for {run.PayPeriod}"
        });

        revVoucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "621102",
            Credit = run.TotalEmployerSsc,
            LocalCredit = run.TotalEmployerSsc,
            BaseCredit = run.TotalEmployerSsc,
            Description = $"Reversal: Employer SSC for {run.PayPeriod}"
        });

        revVoucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "213101",
            Debit = run.TotalNetSalary,
            LocalDebit = run.TotalNetSalary,
            BaseDebit = run.TotalNetSalary,
            Description = $"Reversal: Net Salaries for {run.PayPeriod}"
        });

        var totalSsc = run.TotalEmployeeSsc + run.TotalEmployerSsc;
        revVoucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "213201",
            Debit = totalSsc,
            LocalDebit = totalSsc,
            BaseDebit = totalSsc,
            Description = $"Reversal: SSC Payable for {run.PayPeriod}"
        });

        var totalTax = run.TotalIncomeTax + run.TotalNationalContribution;
        if (totalTax > 0)
        {
            revVoucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "214203",
                Debit = totalTax,
                LocalDebit = totalTax,
                BaseDebit = totalTax,
                Description = $"Reversal: Tax Withheld for {run.PayPeriod}"
            });
        }

        if (run.TotalOtherDeductions > 0)
        {
            revVoucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "213202",
                Debit = run.TotalOtherDeductions,
                LocalDebit = run.TotalOtherDeductions,
                BaseDebit = run.TotalOtherDeductions,
                Description = $"Reversal: Other Deductions for {run.PayPeriod}"
            });
        }

        await _glVoucherRepository.AddVoucherAsync(revVoucher);
        await _glVoucherRepository.SaveChangesAsync();

        run.Status = "REVERSED";
        run.UpdateUser = currentUser;
        run.UpdateDate = now;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Reversed payroll run #{Id} with Reversal JV #{VoucherId}", id, revVoucher.Id);
        return (await GetRunByIdAsync(id))!;
    }

    public async Task<PayrollRunDto> DisbursePayrollAsync(long id, string currentUser)
    {
        var run = await _payrollRepository.GetRunByIdAsync(id, includeLines: false);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({id}) غير موجود.", "PAYROLL_RUN_NOT_FOUND");
        }

        if (run.Status != "POSTED")
        {
            throw new HrValidationException($"يجب ترحيل مسير الرواتب للحسابات العامة قبل الصرف. الحالة الحالية: ({run.Status}).", "RUN_NOT_POSTED");
        }

        run.Status = "PAID";
        run.PaidBy = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        run.PaymentDate = DateTime.UtcNow;
        run.UpdateUser = run.PaidBy;
        run.UpdateDate = DateTime.UtcNow;

        _payrollRepository.UpdateRun(run);
        await _payrollRepository.SaveChangesAsync();

        _logger.LogInformation("Disbursed payroll run #{Id} by {User}", id, currentUser);
        return (await GetRunByIdAsync(id))!;
    }

    public async Task<List<PayslipDto>> GetEmployeePayslipsAsync(string employeeCode, string? payPeriod = null)
    {
        var list = await _payrollRepository.GetEmployeePayslipsAsync(employeeCode, payPeriod);
        return list.Select(MapToPayslipDto).ToList();
    }

    public async Task<PayslipDto?> GetEmployeePayslipForPeriodAsync(string employeeCode, string payPeriod)
    {
        var line = await _payrollRepository.GetEmployeePayslipForPeriodAsync(employeeCode, payPeriod);
        return line == null ? null : MapToPayslipDto(line);
    }

    private static PayrollRunDto MapToRunDto(PayrollRun r) => new()
    {
        Id = r.Id,
        PayPeriod = r.PayPeriod,
        BranchId = r.BranchId,
        RunDate = r.RunDate,
        TotalGrossSalary = r.TotalGrossSalary,
        TotalNetSalary = r.TotalNetSalary,
        TotalEmployeeSsc = r.TotalEmployeeSsc,
        TotalEmployerSsc = r.TotalEmployerSsc,
        TotalIncomeTax = r.TotalIncomeTax,
        TotalNationalContribution = r.TotalNationalContribution,
        TotalOtherDeductions = r.TotalOtherDeductions,
        Status = r.Status,
        JournalVoucherId = r.JournalVoucherId,
        CalculatedBy = r.CalculatedBy,
        CalculationDate = r.CalculationDate,
        ApprovedBy = r.ApprovedBy,
        ApprovalDate = r.ApprovalDate,
        PostedBy = r.PostedBy,
        PostDate = r.PostDate,
        PaidBy = r.PaidBy,
        PaymentDate = r.PaymentDate,
        EmployeeCount = r.Lines.Count,
        Lines = r.Lines.Select(l => new PayrollRunLineDto
        {
            Id = l.Id,
            PayrollRunId = l.PayrollRunId,
            EmployeeCode = l.EmployeeCode,
            EmployeeNameAr = l.Employee?.NameAr ?? string.Empty,
            EmployeeNameEn = l.Employee?.NameEn ?? string.Empty,
            DepartmentCode = l.DepartmentCode,
            CostCenterCode = l.CostCenterCode,
            BasicSalary = l.BasicSalary,
            GrossSalary = l.GrossSalary,
            TotalEarnings = l.TotalEarnings,
            SscEligibleSalary = l.SscEligibleSalary,
            SscEmployeeContribution = l.SscEmployeeContribution,
            SscEmployerContribution = l.SscEmployerContribution,
            TaxableGross = l.TaxableGross,
            AnnualTaxableNet = l.AnnualTaxableNet,
            AnnualExemptions = l.AnnualExemptions,
            IncomeTaxWithheld = l.IncomeTaxWithheld,
            NationalContributionWithheld = l.NationalContributionWithheld,
            OtherDeductions = l.OtherDeductions,
            TotalDeductions = l.TotalDeductions,
            NetPay = l.NetPay,
            PaymentMethod = l.PaymentMethod,
            BankCode = l.BankCode,
            Iban = l.Iban,
            Status = l.Status,
            Components = l.Components.Select(c => new PayrollRunLineComponentDto
            {
                Id = c.Id,
                ComponentCode = c.ComponentCode,
                ComponentNameEn = c.ComponentNameEn,
                ComponentNameAr = c.ComponentNameAr,
                ComponentType = c.ComponentType,
                Amount = c.Amount,
                GlAccountCode = c.GlAccountCode
            }).ToList()
        }).ToList()
    };

    private static PayslipDto MapToPayslipDto(PayrollRunLine l) => new()
    {
        LineId = l.Id,
        PayPeriod = l.PayrollRun?.PayPeriod ?? string.Empty,
        EmployeeCode = l.EmployeeCode,
        EmployeeNameAr = l.Employee?.NameAr ?? string.Empty,
        EmployeeNameEn = l.Employee?.NameEn ?? string.Empty,
        DepartmentName = l.Employee?.Department?.NameAr ?? l.DepartmentCode,
        PositionTitle = l.Employee?.Position?.TitleAr ?? l.Employee?.Position?.TitleEn,
        SscNumber = l.Employee?.SscNumber,
        NationalId = l.Employee?.NationalId,
        BasicSalary = l.BasicSalary,
        GrossSalary = l.GrossSalary,
        SscEmployeeContribution = l.SscEmployeeContribution,
        SscEmployerContribution = l.SscEmployerContribution,
        IncomeTaxWithheld = l.IncomeTaxWithheld,
        NationalContributionWithheld = l.NationalContributionWithheld,
        OtherDeductions = l.OtherDeductions,
        TotalDeductions = l.TotalDeductions,
        NetPay = l.NetPay,
        BankCode = l.BankCode,
        Iban = l.Iban,
        Earnings = l.Components.Where(c => c.ComponentType == "EARNING").Select(c => new PayrollRunLineComponentDto
        {
            Id = c.Id,
            ComponentCode = c.ComponentCode,
            ComponentNameEn = c.ComponentNameEn,
            ComponentNameAr = c.ComponentNameAr,
            ComponentType = c.ComponentType,
            Amount = c.Amount,
            GlAccountCode = c.GlAccountCode
        }).ToList(),
        Deductions = l.Components.Where(c => c.ComponentType == "DEDUCTION").Select(c => new PayrollRunLineComponentDto
        {
            Id = c.Id,
            ComponentCode = c.ComponentCode,
            ComponentNameEn = c.ComponentNameEn,
            ComponentNameAr = c.ComponentNameAr,
            ComponentType = c.ComponentType,
            Amount = c.Amount,
            GlAccountCode = c.GlAccountCode
        }).ToList()
    };
}
