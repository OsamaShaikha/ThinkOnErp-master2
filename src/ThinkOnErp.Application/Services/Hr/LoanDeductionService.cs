using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record LoanDeductionCalculationResult(
    decimal TotalScheduledDeduction,
    decimal TotalActualDeduction,
    decimal TotalCarriedForward,
    List<LoanRepaymentSchedule> UpdatedSchedules,
    List<EmployeeAdvance> UpdatedAdvances
);

public interface ILoanDeductionService
{
    Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsAsync(
        long? runLineId,
        string employeeCode,
        string payPeriod,
        decimal netPayBeforeLoans,
        DeductionPolicy? policy,
        bool persistChanges = false,
        CancellationToken cancellationToken = default);

    Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> CreateAdvanceRequestAsync(CreateAdvanceRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default);
}

public sealed class LoanDeductionService : ILoanDeductionService
{
    private readonly ILoanRepository _loanRepository;

    public LoanDeductionService(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsAsync(
        long? runLineId,
        string employeeCode,
        string payPeriod,
        decimal netPayBeforeLoans,
        DeductionPolicy? policy,
        bool persistChanges = false,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch pending schedules and approved advances for this period
        var allSchedules = await _loanRepository.GetPendingSchedulesForPeriodAsync(payPeriod, cancellationToken);
        var empSchedules = allSchedules.Where(s => s.EmployeeLoan?.EmployeeCode == employeeCode && s.Status == "PENDING").ToList();

        var allAdvances = await _loanRepository.GetAdvancesAsync(employeeCode, payPeriod, cancellationToken);
        var empAdvances = allAdvances.Where(a => a.Status == "APPROVED" && a.RemainingBalance > 0).ToList();

        decimal totalScheduledLoans = empSchedules.Sum(s => s.ScheduledAmount);
        decimal totalScheduledAdvances = empAdvances.Sum(a => a.RemainingBalance);
        decimal totalScheduled = totalScheduledLoans + totalScheduledAdvances;

        if (totalScheduled <= 0)
        {
            return new LoanDeductionCalculationResult(0, 0, 0, new(), new());
        }

        // 2. Determine max allowable deduction cap based on DeductionPolicy
        decimal maxAllowableDeduction = totalScheduled;

        if (policy != null)
        {
            // Max deduction based on percentage of net pay (e.g. 50%)
            decimal percentCap = netPayBeforeLoans * (policy.MaxDeductionPercentage / 100m);
            // Minimum net pay guarantee (e.g. at least 150 JOD left)
            decimal guaranteeCap = Math.Max(0m, netPayBeforeLoans - policy.MinNetPayGuarantee);

            maxAllowableDeduction = Math.Min(percentCap, guaranteeCap);
            if (maxAllowableDeduction < 0) maxAllowableDeduction = 0;

            if (!policy.AllowNegativeNetPay && maxAllowableDeduction > netPayBeforeLoans)
            {
                maxAllowableDeduction = netPayBeforeLoans;
            }
        }

        decimal actualDeductionTotal = 0m;
        decimal remainingCap = maxAllowableDeduction;

        var updatedSchedules = new List<LoanRepaymentSchedule>();
        var updatedAdvances = new List<EmployeeAdvance>();

        // 3. Priority 1: Salary Advances
        foreach (var advance in empAdvances)
        {
            decimal due = advance.RemainingBalance;
            decimal deduct = Math.Min(due, remainingCap);

            if (deduct > 0)
            {
                advance.DeductedAmount += deduct;
                advance.RemainingBalance -= deduct;
                if (advance.RemainingBalance <= 0)
                {
                    advance.Status = "RECOVERED";
                }
                advance.UpdateDate = DateTime.UtcNow;
                updatedAdvances.Add(advance);

                remainingCap -= deduct;
                actualDeductionTotal += deduct;
            }
        }

        // 4. Priority 2: Scheduled Loan Repayments
        foreach (var schedule in empSchedules)
        {
            decimal due = schedule.ScheduledAmount;
            decimal deduct = Math.Min(due, remainingCap);

            schedule.PaidAmount = deduct;
            schedule.CarriedForwardAmount = due - deduct;
            schedule.PayrollRunLineId = runLineId;
            schedule.PaidDate = DateTime.UtcNow;

            if (deduct >= due)
            {
                schedule.Status = "PAID";
            }
            else if (deduct > 0)
            {
                schedule.Status = "PARTIAL";
            }
            else
            {
                schedule.Status = "SKIPPED";
            }

            if (schedule.EmployeeLoan != null)
            {
                schedule.EmployeeLoan.TotalPaidAmount += deduct;
                schedule.EmployeeLoan.RemainingBalance -= deduct;
                if (schedule.EmployeeLoan.RemainingBalance <= 0)
                {
                    schedule.EmployeeLoan.Status = "COMPLETED";
                }
            }

            remainingCap -= deduct;
            actualDeductionTotal += deduct;
            updatedSchedules.Add(schedule);
        }

        decimal carriedForwardTotal = totalScheduled - actualDeductionTotal;

        if (persistChanges)
        {
            if (updatedSchedules.Count > 0)
            {
                await _loanRepository.UpdateSchedulesAsync(updatedSchedules, cancellationToken);
            }
            foreach (var adv in updatedAdvances)
            {
                await _loanRepository.UpdateAdvanceAsync(adv, cancellationToken);
            }
            await _loanRepository.SaveChangesAsync(cancellationToken);
        }

        return new LoanDeductionCalculationResult(
            totalScheduled,
            actualDeductionTotal,
            carriedForwardTotal,
            updatedSchedules,
            updatedAdvances
        );
    }

    public async Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default)
    {
        var loan = new EmployeeLoan
        {
            EmployeeCode = dto.EmployeeCode,
            LoanType = dto.LoanType,
            PrincipalAmount = dto.PrincipalAmount,
            MonthlyInstallmentAmount = dto.TotalInstallments > 0 ? Math.Round(dto.PrincipalAmount / dto.TotalInstallments, 3) : dto.PrincipalAmount,
            TotalInstallments = dto.TotalInstallments,
            TotalPaidAmount = 0m,
            RemainingBalance = dto.PrincipalAmount,
            StartDate = dto.StartDate,
            EndDate = dto.StartDate.AddMonths(dto.TotalInstallments),
            Status = "PENDING",
            Notes = dto.Notes,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        // Generate repayment schedules
        var currentPeriodDate = dto.StartDate;
        for (int i = 1; i <= dto.TotalInstallments; i++)
        {
            var periodCode = currentPeriodDate.ToString("yyyy-MM");
            decimal instAmount = (i == dto.TotalInstallments)
                ? (loan.PrincipalAmount - (loan.MonthlyInstallmentAmount * (dto.TotalInstallments - 1)))
                : loan.MonthlyInstallmentAmount;

            loan.Schedules.Add(new LoanRepaymentSchedule
            {
                InstallmentNo = i,
                PayPeriod = periodCode,
                ScheduledAmount = instAmount,
                PaidAmount = 0,
                CarriedForwardAmount = 0,
                Status = "PENDING"
            });

            currentPeriodDate = currentPeriodDate.AddMonths(1);
        }

        await _loanRepository.AddLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default)
    {
        var loan = await _loanRepository.GetLoanByIdAsync(loanId, cancellationToken);
        if (loan == null) throw new KeyNotFoundException($"Loan with ID {loanId} not found.");

        loan.Status = "ACTIVE";
        loan.ApprovedBy = user;
        loan.ApprovalDate = DateTime.UtcNow;
        loan.UpdateUser = user;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task<EmployeeAdvance> CreateAdvanceRequestAsync(CreateAdvanceRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        var advance = new EmployeeAdvance
        {
            EmployeeCode = dto.EmployeeCode,
            AdvanceAmount = dto.AdvanceAmount,
            TargetPayPeriod = dto.TargetPayPeriod,
            DeductedAmount = 0m,
            RemainingBalance = dto.AdvanceAmount,
            Status = "PENDING",
            Reason = dto.Reason,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _loanRepository.AddAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return advance;
    }

    public async Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default)
    {
        var advance = await _loanRepository.GetAdvanceByIdAsync(advanceId, cancellationToken);
        if (advance == null) throw new KeyNotFoundException($"Advance with ID {advanceId} not found.");

        advance.Status = "APPROVED";
        advance.ApprovedBy = user;
        advance.ApprovalDate = DateTime.UtcNow;
        advance.UpdateUser = user;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return advance;
    }

    public async Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        return await _loanRepository.GetLoansAsync(employeeCode, status, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default)
    {
        return await _loanRepository.GetAdvancesAsync(employeeCode, targetPayPeriod, cancellationToken);
    }
}
