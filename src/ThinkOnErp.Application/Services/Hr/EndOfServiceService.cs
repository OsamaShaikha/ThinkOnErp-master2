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

public sealed class EndOfServiceService : IEndOfServiceService
{
    private readonly IEndOfServiceRepository _eosRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICompensationRepository _compensationRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IGlVoucherRepository _glVoucherRepository;
    private readonly ILogger<EndOfServiceService> _logger;

    public EndOfServiceService(
        IEndOfServiceRepository eosRepository,
        IEmployeeRepository employeeRepository,
        ICompensationRepository compensationRepository,
        ILeaveRepository leaveRepository,
        IGlVoucherRepository glVoucherRepository,
        ILogger<EndOfServiceService> logger)
    {
        _eosRepository = eosRepository ?? throw new ArgumentNullException(nameof(eosRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _glVoucherRepository = glVoucherRepository ?? throw new ArgumentNullException(nameof(glVoucherRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<EndOfServiceProvisionDto>> GetEmployeeProvisionsAsync(string employeeCode)
    {
        var list = await _eosRepository.GetProvisionsByEmployeeAsync(employeeCode);
        return list.Select(MapToProvisionDto).ToList();
    }

    public async Task<RunProvisionAccrualResultDto> RunMonthlyProvisionAccrualAsync(string payPeriod, string currentUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payPeriod);

        var parts = payPeriod.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month))
        {
            throw new HrValidationException($"صيغة فترة الراتب غير صحيحة ({payPeriod}). الصيغة المطلوبة هي YYYY-MM.", "INVALID_PAY_PERIOD");
        }

        var periodDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var activeEmployees = await _employeeRepository.GetAllAsync(activeOnly: true);
        var eligibleEmployees = activeEmployees.Where(e => e.EmploymentStatus != "TERMINATED").ToList();

        var totalAccrued = 0m;
        var processedCount = 0;
        var user = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM_EOS" : currentUser;

        foreach (var emp in eligibleEmployees)
        {
            var existing = await _eosRepository.GetProvisionAsync(emp.EmployeeCode, payPeriod);
            if (existing != null)
            {
                continue; // Already processed for this period
            }

            var structure = await _compensationRepository.GetActiveStructureAsync(emp.EmployeeCode, periodDate);
            if (structure == null || structure.BasicSalary <= 0)
            {
                continue;
            }

            var serviceYears = (decimal)Math.Round((periodDate - emp.HireDate).TotalDays / 365.25, 2);
            // Standard Jordan monthly provision accrual = 1 month of basic salary / 12 months
            var monthlyAccrual = Math.Round(structure.BasicSalary / 12m, 3);

            var prevProvisions = await _eosRepository.GetProvisionsByEmployeeAsync(emp.EmployeeCode);
            var lastAccumulated = prevProvisions.FirstOrDefault()?.TotalAccumulatedProvision ?? 0m;

            var provision = new EndOfServiceProvisionAccrual
            {
                EmployeeCode = emp.EmployeeCode,
                PayPeriod = payPeriod,
                BasicSalary = structure.BasicSalary,
                ServiceYears = serviceYears,
                MonthlyAccrualAmount = monthlyAccrual,
                TotalAccumulatedProvision = lastAccumulated + monthlyAccrual,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };

            await _eosRepository.AddProvisionAsync(provision);
            totalAccrued += monthlyAccrual;
            processedCount++;
        }

        long? voucherId = null;

        // Create and post GL Journal Voucher if accruals were generated
        if (totalAccrued > 0)
        {
            var now = DateTime.UtcNow;
            var nextVoucherNo = await _glVoucherRepository.GenerateNextSerialNoAsync(1, year, month, 1, "YEARLY");

            var voucher = new GlVoucherHeader
            {
                BranchId = 1,
                FiscalYearId = year,
                VoucherYear = year,
                VoucherMonth = month,
                VoucherType = 1,
                VoucherNo = nextVoucherNo,
                VoucherDate = now,
                Description = $"Monthly End-of-Service Provision Accrual for {payPeriod}",
                TotalAmount = totalAccrued,
                TotalLocalDebit = totalAccrued,
                TotalLocalCredit = totalAccrued,
                Status = 3, // Posted
                IsAutoRecord = true,
                SourceSystemCode = "HR_EOS_ACCRUAL",
                CreationUser = user,
                CreationDate = now,
                PostUser = user,
                PostDate = now
            };

            // Debit: 621101 Salaries / End of Service Provision Expense
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = 1,
                AccountCode = "621101",
                Debit = totalAccrued,
                LocalDebit = totalAccrued,
                BaseDebit = totalAccrued,
                Description = $"End-of-Service Expense Accrual for {payPeriod}"
            });

            // Credit: 223101 End-of-Service Provision Liability
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = 2,
                AccountCode = "223101",
                Credit = totalAccrued,
                LocalCredit = totalAccrued,
                BaseCredit = totalAccrued,
                Description = $"End-of-Service Provision Accrual for {payPeriod}"
            });

            await _glVoucherRepository.AddVoucherAsync(voucher);
            await _glVoucherRepository.SaveChangesAsync();
            voucherId = voucher.Id;
        }

        await _eosRepository.SaveChangesAsync();

        _logger.LogInformation("Ran monthly EOS provision accrual for {Period}: {Count} employees, total {Amount} JOD", payPeriod, processedCount, totalAccrued);

        return new RunProvisionAccrualResultDto
        {
            ProcessedEmployees = processedCount,
            TotalAccruedAmount = totalAccrued,
            PayPeriod = payPeriod,
            JournalVoucherId = voucherId,
            Message = $"Processed EOS provision accrual for {processedCount} employees. Total accrued: {totalAccrued} JOD."
        };
    }

    public async Task<List<FinalSettlementDto>> GetAllSettlementsAsync(string? status = null)
    {
        var list = await _eosRepository.GetAllSettlementsAsync(status);
        return list.Select(MapToSettlementDto).ToList();
    }

    public async Task<FinalSettlementDto?> GetSettlementByIdAsync(long id)
    {
        var settlement = await _eosRepository.GetSettlementByIdAsync(id);
        return settlement == null ? null : MapToSettlementDto(settlement);
    }

    public async Task<FinalSettlementDto?> GetSettlementByEmployeeAsync(string employeeCode)
    {
        var settlement = await _eosRepository.GetSettlementByEmployeeAsync(employeeCode);
        return settlement == null ? null : MapToSettlementDto(settlement);
    }

    public async Task<FinalSettlementDto> CalculateFinalSettlementAsync(CalculateFinalSettlementDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var employee = await _employeeRepository.GetByCodeAsync(empCode, includeDetails: true);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var termDate = dto.TerminationDate.Date;
        var structure = await _compensationRepository.GetActiveStructureAsync(empCode, termDate);
        var basicSalary = structure?.BasicSalary ?? 0m;

        // Service years
        var serviceYears = (decimal)Math.Max(0, (termDate - employee.HireDate).TotalDays / 365.25);

        // Gratuity calculation (1 month basic per year)
        var gratuity = Math.Round(serviceYears * basicSalary, 3);

        // Unused leave encashment
        var leaveBalances = await _leaveRepository.GetBalancesByEmployeeAsync(empCode, termDate.Year);
        var annualLeaveBalance = leaveBalances.FirstOrDefault(b => b.LeaveTypeCode == "ANNUAL");
        var unusedDays = annualLeaveBalance?.RemainingDays ?? 0m;
        var dailyRate = basicSalary / 30m;
        var leaveEncashment = Math.Max(0, Math.Round(unusedDays * dailyRate, 3));

        var settlement = new FinalSettlement
        {
            EmployeeCode = empCode,
            TerminationDate = termDate,
            TerminationReason = dto.TerminationReason.Trim().ToUpperInvariant(),
            ServiceYears = Math.Round(serviceYears, 2),
            LastBasicSalary = basicSalary,
            EndOfServiceGratuity = gratuity,
            UnusedLeaveDays = unusedDays,
            UnusedLeaveEncashment = leaveEncashment,
            NoticePeriodPay = dto.NoticePeriodPay ?? 0m,
            OtherEntitlements = dto.OtherEntitlements ?? 0m,
            LoanBalanceDeduction = dto.LoanBalanceDeduction ?? 0m,
            OtherDeductions = dto.OtherDeductions ?? 0m,
            Status = "DRAFT",
            Notes = dto.Notes,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _eosRepository.AddSettlementAsync(settlement);
        await _eosRepository.SaveChangesAsync();

        _logger.LogInformation("Calculated final settlement for {Emp}: Entitlements {Ent}, Deductions {Ded}, Net {Net}", empCode, settlement.TotalEntitlements, settlement.TotalDeductions, settlement.NetSettlementAmount);

        return (await GetSettlementByIdAsync(settlement.Id))!;
    }

    public async Task<FinalSettlementDto> ApproveFinalSettlementAsync(long id, string approvedBy)
    {
        var settlement = await _eosRepository.GetSettlementByIdAsync(id);
        if (settlement == null)
        {
            throw new HrNotFoundException($"مخالصة نهاية الخدمة رقم ({id}) غير موجودة.", "SETTLEMENT_NOT_FOUND");
        }

        if (settlement.Status != "DRAFT")
        {
            throw new HrValidationException($"لا يمكن اعتماد مخالصة نهاية خدمة بحالة ({settlement.Status}).", "INVALID_STATUS");
        }

        settlement.Status = "APPROVED";
        settlement.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        settlement.ApprovalDate = DateTime.UtcNow;
        settlement.UpdateUser = settlement.ApprovedBy;
        settlement.UpdateDate = DateTime.UtcNow;

        _eosRepository.UpdateSettlement(settlement);
        await _eosRepository.SaveChangesAsync();

        _logger.LogInformation("Approved final settlement #{Id} by {User}", id, approvedBy);
        return MapToSettlementDto(settlement);
    }

    public async Task<FinalSettlementDto> PostFinalSettlementToGlAsync(long id, string currentUser)
    {
        var settlement = await _eosRepository.GetSettlementByIdAsync(id);
        if (settlement == null)
        {
            throw new HrNotFoundException($"مخالصة نهاية الخدمة رقم ({id}) غير موجودة.", "SETTLEMENT_NOT_FOUND");
        }

        if (settlement.Status != "APPROVED")
        {
            throw new HrValidationException($"يجب اعتماد المخالصة قبل ترحيلها للحسابات العامة. الحالة الحالية: ({settlement.Status}).", "SETTLEMENT_NOT_APPROVED");
        }

        if (settlement.JournalVoucherId.HasValue)
        {
            throw new HrConflictException($"تم ترحيل المخالصة مسبقاً بسند قيد رقم ({settlement.JournalVoucherId}).", "ALREADY_POSTED");
        }

        var now = DateTime.UtcNow;
        var nextVoucherNo = await _glVoucherRepository.GenerateNextSerialNoAsync(1, now.Year, now.Month, 1, "YEARLY");

        var voucher = new GlVoucherHeader
        {
            BranchId = 1,
            FiscalYearId = now.Year,
            VoucherYear = now.Year,
            VoucherMonth = now.Month,
            VoucherType = 1,
            VoucherNo = nextVoucherNo,
            VoucherDate = now,
            Description = $"Final Settlement GL Posting for Employee {settlement.EmployeeCode}",
            TotalAmount = settlement.TotalEntitlements,
            TotalLocalDebit = settlement.TotalEntitlements,
            TotalLocalCredit = settlement.TotalEntitlements,
            Status = 3, // Posted
            IsAutoRecord = true,
            SourceSystemCode = "HR_FINAL_SETTLEMENT",
            SourceRefId = settlement.Id,
            CreationUser = currentUser,
            CreationDate = now,
            PostUser = currentUser,
            PostDate = now
        };

        var lineSer = 1;

        // Debit: 223101 End-of-Service Provision (Gratuity portion)
        if (settlement.EndOfServiceGratuity > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "223101",
                Debit = settlement.EndOfServiceGratuity,
                LocalDebit = settlement.EndOfServiceGratuity,
                BaseDebit = settlement.EndOfServiceGratuity,
                Description = $"EOS Gratuity for {settlement.EmployeeCode}"
            });
        }

        // Debit: 621101 Salaries & Wages (Leave encashment + Notice pay + other)
        var otherEntitlements = settlement.UnusedLeaveEncashment + settlement.NoticePeriodPay + settlement.OtherEntitlements;
        if (otherEntitlements > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "621101",
                Debit = otherEntitlements,
                LocalDebit = otherEntitlements,
                BaseDebit = otherEntitlements,
                Description = $"Leave Encashment / Notice Pay for {settlement.EmployeeCode}"
            });
        }

        // Credit: 213101 Salaries / Settlement Payable (Net settlement)
        voucher.Details.Add(new GlVoucherDetail
        {
            LineSer = lineSer++,
            AccountCode = "213101",
            Credit = settlement.NetSettlementAmount,
            LocalCredit = settlement.NetSettlementAmount,
            BaseCredit = settlement.NetSettlementAmount,
            Description = $"Net Final Settlement Payable for {settlement.EmployeeCode}"
        });

        // Credit: 113101 Employee Advances / Loan Recovery
        if (settlement.TotalDeductions > 0)
        {
            voucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = "113101",
                Credit = settlement.TotalDeductions,
                LocalCredit = settlement.TotalDeductions,
                BaseCredit = settlement.TotalDeductions,
                Description = $"Loan & Deductions Recovery for {settlement.EmployeeCode}"
            });
        }

        await _glVoucherRepository.AddVoucherAsync(voucher);
        await _glVoucherRepository.SaveChangesAsync();

        settlement.Status = "POSTED";
        settlement.JournalVoucherId = voucher.Id;
        settlement.UpdateUser = currentUser;
        settlement.UpdateDate = now;

        _eosRepository.UpdateSettlement(settlement);
        await _eosRepository.SaveChangesAsync();

        _logger.LogInformation("Posted final settlement #{Id} to GL with Journal Voucher #{VoucherId}", id, voucher.Id);
        return MapToSettlementDto(settlement);
    }

    private static EndOfServiceProvisionDto MapToProvisionDto(EndOfServiceProvisionAccrual p)
    {
        return new EndOfServiceProvisionDto
        {
            Id = p.Id,
            EmployeeCode = p.EmployeeCode,
            EmployeeNameEn = p.Employee?.NameEn ?? string.Empty,
            PayPeriod = p.PayPeriod,
            BasicSalary = p.BasicSalary,
            ServiceYears = p.ServiceYears,
            MonthlyAccrualAmount = p.MonthlyAccrualAmount,
            TotalAccumulatedProvision = p.TotalAccumulatedProvision,
            JournalVoucherId = p.JournalVoucherId,
            CreationDate = p.CreationDate
        };
    }

    private static FinalSettlementDto MapToSettlementDto(FinalSettlement s)
    {
        return new FinalSettlementDto
        {
            Id = s.Id,
            EmployeeCode = s.EmployeeCode,
            EmployeeNameAr = s.Employee?.NameAr ?? string.Empty,
            EmployeeNameEn = s.Employee?.NameEn ?? string.Empty,
            TerminationDate = s.TerminationDate,
            TerminationReason = s.TerminationReason,
            ServiceYears = s.ServiceYears,
            LastBasicSalary = s.LastBasicSalary,
            EndOfServiceGratuity = s.EndOfServiceGratuity,
            UnusedLeaveDays = s.UnusedLeaveDays,
            UnusedLeaveEncashment = s.UnusedLeaveEncashment,
            NoticePeriodPay = s.NoticePeriodPay,
            OtherEntitlements = s.OtherEntitlements,
            TotalEntitlements = s.TotalEntitlements,
            LoanBalanceDeduction = s.LoanBalanceDeduction,
            OtherDeductions = s.OtherDeductions,
            TotalDeductions = s.TotalDeductions,
            NetSettlementAmount = s.NetSettlementAmount,
            Status = s.Status,
            JournalVoucherId = s.JournalVoucherId,
            ApprovedBy = s.ApprovedBy,
            ApprovalDate = s.ApprovalDate,
            Notes = s.Notes
        };
    }
}
