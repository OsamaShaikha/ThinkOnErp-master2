using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class LoanDeductionService : ILoanDeductionService
{
    private readonly ILoanRepository _loanRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILogger<LoanDeductionService> _logger;

    public LoanDeductionService(
        ILoanRepository loanRepo,
        IPolicyRepository policyRepo,
        IEmployeeRepository employeeRepo,
        ILogger<LoanDeductionService> logger)
    {
        _loanRepo = loanRepo ?? throw new ArgumentNullException(nameof(loanRepo));
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LoanDeductionResult> ProcessPeriodLoanDeductionsAsync(
        string employeeCode,
        string payPeriod,
        decimal disposableSalary,
        long companyId,
        DateTime calculationDate)
    {
        var items = new List<DeductedLoanItem>();
        if (disposableSalary <= 0)
        {
            return new LoanDeductionResult(0m, 0m, items);
        }

        // 1. Resolve company deduction policy
        var policy = await _policyRepo.GetEffectiveDeductionPolicyAsync(companyId, calculationDate);
        var maxDeductionPct = policy?.MaxDeductionPercentage ?? 50.0m;
        var minNetGuarantee = policy?.MinimumNetPayGuarantee ?? 0m;
        var autoCarryForward = policy?.AutoCapAndCarryForward ?? true;

        var maxAllowedDeduction = Math.Max(0, (disposableSalary * (maxDeductionPct / 100m)) - minNetGuarantee);
        var remainingCapacity = maxAllowedDeduction;

        decimal totalDeducted = 0m;
        decimal totalCarriedFwd = 0m;

        // 2. Fetch pending salary advances for this period
        var advances = await _loanRepo.GetAdvancesAsync(employeeCode, payPeriod, status: "APPROVED");
        foreach (var adv in advances)
        {
            if (adv.RemainingBalance <= 0) continue;

            var toDeduct = Math.Min(adv.RemainingBalance, remainingCapacity);
            var carried = adv.RemainingBalance - toDeduct;

            adv.DeductedAmount += toDeduct;
            adv.RemainingBalance -= toDeduct;
            if (adv.RemainingBalance <= 0) adv.Status = "DEDUCTED";

            _loanRepo.UpdateAdvance(adv);

            remainingCapacity -= toDeduct;
            totalDeducted += toDeduct;
            totalCarriedFwd += carried;

            items.Add(new DeductedLoanItem(
                LoanId: adv.Id,
                ScheduleId: null,
                LoanType: "SALARY_ADVANCE",
                ScheduledAmount: adv.AdvanceAmount,
                DeductedAmount: toDeduct,
                CarriedForwardAmount: carried));
        }

        // 3. Fetch scheduled loan installments for this period
        var allSchedules = await _loanRepo.GetPendingSchedulesByPeriodAsync(payPeriod) ?? new List<LoanRepaymentSchedule>();
        var employeeSchedules = allSchedules.Where(s => s.EmployeeLoan?.EmployeeCode == employeeCode).ToList();

        foreach (var sch in employeeSchedules)
        {
            var loan = sch.EmployeeLoan!;
            var scheduledAmount = sch.ScheduledAmount + sch.CarriedForwardAmount;

            var toDeduct = Math.Min(scheduledAmount, remainingCapacity);
            var unPaid = scheduledAmount - toDeduct;

            sch.PaidAmount = toDeduct;
            sch.PaidDate = DateTime.UtcNow;

            if (unPaid > 0 && autoCarryForward)
            {
                sch.Status = "PARTIAL";
                sch.CarriedForwardAmount = unPaid;

                // Carry forward remainder to next schedule installment if exists
                var nextSchedules = await _loanRepo.GetSchedulesByLoanIdAsync(loan.Id) ?? new List<LoanRepaymentSchedule>();
                var nextSch = nextSchedules.FirstOrDefault(s => s.InstallmentNo == sch.InstallmentNo + 1);
                if (nextSch != null)
                {
                    nextSch.CarriedForwardAmount += unPaid;
                    _loanRepo.UpdateSchedule(nextSch);
                }
            }
            else
            {
                sch.Status = "PAID";
            }

            loan.TotalPaidAmount += toDeduct;
            loan.RemainingBalance = Math.Max(0, loan.RemainingBalance - toDeduct);
            if (loan.RemainingBalance <= 0) loan.Status = "COMPLETED";

            _loanRepo.UpdateSchedule(sch);
            _loanRepo.UpdateLoan(loan);

            remainingCapacity -= toDeduct;
            totalDeducted += toDeduct;
            totalCarriedFwd += unPaid;

            items.Add(new DeductedLoanItem(
                LoanId: loan.Id,
                ScheduleId: sch.Id,
                LoanType: loan.LoanType,
                ScheduledAmount: scheduledAmount,
                DeductedAmount: toDeduct,
                CarriedForwardAmount: unPaid));
        }

        await _loanRepo.SaveChangesAsync();

        return new LoanDeductionResult(
            TotalDeductedAmount: Math.Round(totalDeducted, 3),
            TotalCarriedForwardAmount: Math.Round(totalCarriedFwd, 3),
            Items: items);
    }

    public async Task<EmployeeLoanDto> CreateLoanAsync(CreateEmployeeLoanDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var employee = await _employeeRepo.GetByCodeAsync(empCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var monthlyInstallment = Math.Round(dto.PrincipalAmount / dto.TotalInstallments, 3);
        var loan = new EmployeeLoan
        {
            EmployeeCode = empCode,
            LoanType = dto.LoanType.ToUpperInvariant(),
            PrincipalAmount = dto.PrincipalAmount,
            TotalInstallments = dto.TotalInstallments,
            MonthlyInstallmentAmount = monthlyInstallment,
            TotalPaidAmount = 0,
            RemainingBalance = dto.PrincipalAmount,
            StartDate = dto.StartDate.Date,
            EndDate = dto.StartDate.Date.AddMonths(dto.TotalInstallments),
            Status = "PENDING",
            Notes = dto.Notes,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        // Build monthly installment schedules
        for (int i = 1; i <= dto.TotalInstallments; i++)
        {
            var instDate = dto.StartDate.Date.AddMonths(i - 1);
            loan.Schedules.Add(new LoanRepaymentSchedule
            {
                InstallmentNo = i,
                PayPeriod = $"{instDate.Year:D4}-{instDate.Month:D2}",
                ScheduledAmount = monthlyInstallment,
                PaidAmount = 0,
                CarriedForwardAmount = 0,
                Status = "PENDING"
            });
        }

        await _loanRepo.AddLoanAsync(loan);
        await _loanRepo.SaveChangesAsync();

        _logger.LogInformation("Created loan for {Emp} with amount {Amount} across {Inst} installments", empCode, dto.PrincipalAmount, dto.TotalInstallments);
        return MapToLoanDto(loan);
    }

    public async Task<EmployeeLoanDto> ApproveLoanAsync(long id, string approvedBy)
    {
        var loan = await _loanRepo.GetLoanByIdAsync(id);
        if (loan == null)
        {
            throw new HrNotFoundException($"القرض رقم ({id}) غير موجود.", "LOAN_NOT_FOUND");
        }

        if (loan.Status != "PENDING")
        {
            throw new HrValidationException($"لا يمكن اعتماد قرض بحالة ({loan.Status}).", "INVALID_STATUS");
        }

        loan.Status = "ACTIVE";
        loan.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        loan.ApprovalDate = DateTime.UtcNow;
        loan.UpdateUser = loan.ApprovedBy;
        loan.UpdateDate = DateTime.UtcNow;

        _loanRepo.UpdateLoan(loan);
        await _loanRepo.SaveChangesAsync();

        return MapToLoanDto(loan);
    }

    public async Task<EmployeeAdvanceDto> CreateAdvanceAsync(CreateEmployeeAdvanceDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var employee = await _employeeRepo.GetByCodeAsync(empCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var advance = new EmployeeAdvance
        {
            EmployeeCode = empCode,
            AdvanceAmount = dto.AdvanceAmount,
            TargetPayPeriod = dto.TargetPayPeriod,
            DeductedAmount = 0,
            RemainingBalance = dto.AdvanceAmount,
            Status = "PENDING",
            Reason = dto.Reason,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _loanRepo.AddAdvanceAsync(advance);
        await _loanRepo.SaveChangesAsync();

        return MapToAdvanceDto(advance);
    }

    public async Task<EmployeeAdvanceDto> ApproveAdvanceAsync(long id, string approvedBy)
    {
        var advance = await _loanRepo.GetAdvanceByIdAsync(id);
        if (advance == null)
        {
            throw new HrNotFoundException($"طلب السلفة رقم ({id}) غير موجود.", "ADVANCE_NOT_FOUND");
        }

        advance.Status = "APPROVED";
        advance.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        advance.ApprovalDate = DateTime.UtcNow;
        advance.UpdateUser = advance.ApprovedBy;
        advance.UpdateDate = DateTime.UtcNow;

        _loanRepo.UpdateAdvance(advance);
        await _loanRepo.SaveChangesAsync();

        return MapToAdvanceDto(advance);
    }

    public async Task<List<EmployeeLoanDto>> GetLoansAsync(string? employeeCode = null, string? status = null)
    {
        var list = await _loanRepo.GetLoansAsync(employeeCode, status);
        return list.Select(MapToLoanDto).ToList();
    }

    public async Task<List<EmployeeAdvanceDto>> GetAdvancesAsync(string? employeeCode = null, string? payPeriod = null, string? status = null)
    {
        var list = await _loanRepo.GetAdvancesAsync(employeeCode, payPeriod, status);
        return list.Select(MapToAdvanceDto).ToList();
    }

    private static EmployeeLoanDto MapToLoanDto(EmployeeLoan l) => new()
    {
        Id = l.Id,
        EmployeeCode = l.EmployeeCode,
        EmployeeName = l.Employee?.NameAr,
        LoanType = l.LoanType,
        PrincipalAmount = l.PrincipalAmount,
        TotalInstallments = l.TotalInstallments,
        MonthlyInstallmentAmount = l.MonthlyInstallmentAmount,
        TotalPaidAmount = l.TotalPaidAmount,
        RemainingBalance = l.RemainingBalance,
        StartDate = l.StartDate,
        EndDate = l.EndDate,
        Status = l.Status,
        ApprovedBy = l.ApprovedBy,
        ApprovalDate = l.ApprovalDate,
        Notes = l.Notes,
        Schedules = l.Schedules.Select(s => new LoanRepaymentScheduleDto
        {
            Id = s.Id,
            EmployeeLoanId = s.EmployeeLoanId,
            InstallmentNo = s.InstallmentNo,
            PayPeriod = s.PayPeriod,
            ScheduledAmount = s.ScheduledAmount,
            PaidAmount = s.PaidAmount,
            CarriedForwardAmount = s.CarriedForwardAmount,
            Status = s.Status,
            PaidDate = s.PaidDate
        }).ToList()
    };

    private static EmployeeAdvanceDto MapToAdvanceDto(EmployeeAdvance a) => new()
    {
        Id = a.Id,
        EmployeeCode = a.EmployeeCode,
        EmployeeName = a.Employee?.NameAr,
        AdvanceAmount = a.AdvanceAmount,
        TargetPayPeriod = a.TargetPayPeriod,
        DeductedAmount = a.DeductedAmount,
        RemainingBalance = a.RemainingBalance,
        Status = a.Status,
        Reason = a.Reason,
        ApprovedBy = a.ApprovedBy,
        ApprovalDate = a.ApprovalDate
    };
}
