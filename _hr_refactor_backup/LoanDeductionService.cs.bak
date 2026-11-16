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
        CancellationToken cancellationToken = default,
        string? payrollCurrencyCode = null);

    Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> CreateAdvanceRequestAsync(CreateAdvanceRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default);
    Task<LoanRepaymentSchedule> SkipInstallmentAsync(long scheduleId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> CancelLoanAsync(long loanId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> CancelAdvanceAsync(long advanceId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> SettleLoanEarlyAsync(long loanId, SettleLoanEarlyDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> SettleAdvanceEarlyAsync(long advanceId, SettleAdvanceEarlyDto dto, string user, CancellationToken cancellationToken = default);
}

public sealed class LoanDeductionService : ILoanDeductionService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IEmployeeRepository? _employeeRepository;

    public LoanDeductionService(ILoanRepository loanRepository, IHrUnitOfWork? unitOfWork = null, IEmployeeRepository? employeeRepository = null)
    {
        _loanRepository = loanRepository;
        _unitOfWork = unitOfWork;
        _employeeRepository = employeeRepository;
    }

    public async Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsAsync(
        long? runLineId,
        string employeeCode,
        string payPeriod,
        decimal netPayBeforeLoans,
        DeductionPolicy? policy,
        bool persistChanges = false,
        CancellationToken cancellationToken = default,
        string? payrollCurrencyCode = null)
    {
        if (persistChanges)
            throw new InvalidOperationException("Loan settlement requires a confirmed Accounting posting receipt; HR calculation and approval cannot persist repayments.");
        // ACTIVE is a legacy eligibility state; INT-08 must establish funding receipts
        // and audit legacy balances before enabling the independent workflow.
        var allSchedules = await _loanRepository.GetPendingSchedulesForPeriodAsync(payPeriod, cancellationToken);
        var empSchedules = allSchedules.Where(s => s.EmployeeLoan is { Status: "ACTIVE" } loan && loan.EmployeeCode == employeeCode &&
            (s.Status == "PENDING" || s.Status == "PARTIAL" || s.Status == "SKIPPED") && s.ReservedByHandoffId == null)
            .OrderBy(s => s.PayPeriod).ThenBy(s => s.InstallmentNo).ToList();

        var allAdvances = await _loanRepository.GetAdvancesAsync(employeeCode, payPeriod, cancellationToken);
        var empAdvances = allAdvances.Where(a => a.Status == "ACTIVE" && a.RemainingBalance > 0 && a.ReservedByHandoffId == null).ToList();

        (int DecimalPlaces, int? RoundingRule)? currencyRules = null;
        if (empSchedules.Count > 0 || empAdvances.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(payrollCurrencyCode))
                throw new InvalidOperationException("Payroll currency is required when active loan or advance deductions are present.");
            var normalizedPayrollCurrency = PayrollCurrencyRules.RequireSingleCurrency([payrollCurrencyCode])
                ?? throw new InvalidOperationException("Payroll currency is required when active loan or advance deductions are present.");
            currencyRules = await _loanRepository.GetCurrencyRulesForEmployeeAsync(
                employeeCode, normalizedPayrollCurrency, cancellationToken);
            foreach (var schedule in empSchedules)
            {
                var loan = schedule.EmployeeLoan
                    ?? throw new InvalidOperationException($"Loan schedule {schedule.Id} has no linked loan currency; resolve the loan data before payroll calculation.");
                var loanCurrency = SalaryPaymentMetadata.Normalize(loan.CurrencyCode);
                if (loanCurrency.Length == 0 || loanCurrency != normalizedPayrollCurrency)
                    throw new InvalidOperationException($"Active loan {loan.Id} must have an explicit currency matching payroll currency {normalizedPayrollCurrency} before payroll calculation.");
                if (schedule.ScheduledAmount < 0m || schedule.PaidAmount < 0m || schedule.PaidAmount > schedule.ScheduledAmount ||
                    !HasCurrencyAmountPrecision(schedule.ScheduledAmount - schedule.PaidAmount, currencyRules.Value.DecimalPlaces))
                    throw new InvalidOperationException($"Active loan schedule {schedule.Id} has an invalid or over-precision due amount; resolve it explicitly before payroll calculation.");
            }

            foreach (var advance in empAdvances)
            {
                var advanceCurrency = SalaryPaymentMetadata.Normalize(advance.CurrencyCode);
                if (advanceCurrency.Length == 0 || advanceCurrency != normalizedPayrollCurrency)
                    throw new InvalidOperationException($"Active advance {advance.Id} must have an explicit currency matching payroll currency {normalizedPayrollCurrency} before payroll calculation.");
                if (!HasCurrencyAmountPrecision(advance.RemainingBalance, currencyRules.Value.DecimalPlaces))
                    throw new InvalidOperationException($"Active advance {advance.Id} has an over-precision balance; resolve it explicitly before payroll calculation.");
            }
        }

        // Preview copies must never mutate EF-tracked balances or repayment state.
        var loans = new Dictionary<long, EmployeeLoan>();
        empSchedules = empSchedules.Select(s =>
        {
            var copy = CopyScalars(s);
            if (s.EmployeeLoan != null)
            {
                if (!loans.TryGetValue(s.EmployeeLoanId, out var loan))
                    loans[s.EmployeeLoanId] = loan = CopyScalars(s.EmployeeLoan);
                copy.EmployeeLoan = loan;
            }
            return copy;
        }).ToList();
        empAdvances = empAdvances.Select(CopyScalars).ToList();

        decimal totalScheduledLoans = empSchedules.Sum(s => Math.Max(0m, s.ScheduledAmount - s.PaidAmount));
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

        // The policy cap can have more fractional digits than the payroll
        // currency (for example, a percentage of a high precision net pay).
        // Rule 4 is DOWN (toward zero), so allocations never exceed the policy
        // cap and every resulting settlement remains payable at currency precision.
        var currencyDecimalPlaces = currencyRules?.DecimalPlaces
            ?? throw new InvalidOperationException("Currency precision is required when active loan or advance deductions are present.");
        maxAllowableDeduction = PayrollCurrencyRules.Round(
            Math.Max(0m, maxAllowableDeduction),
            currencyDecimalPlaces,
            4);

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
            decimal due = Math.Max(0m, schedule.ScheduledAmount - schedule.PaidAmount);
            decimal deduct = Math.Min(due, remainingCap);

            schedule.PaidAmount += deduct;
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

        return new LoanDeductionCalculationResult(
            totalScheduled,
            actualDeductionTotal,
            carriedForwardTotal,
            updatedSchedules,
            updatedAdvances
        );
    }

    private static T CopyScalars<T>(T source) where T : class, new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties().Where(p => p.CanRead && p.CanWrite &&
            (p.PropertyType.IsValueType || p.PropertyType == typeof(string))))
            property.SetValue(copy, property.GetValue(source));
        return copy;
    }

    private static string RequireCurrencyCode(string? value, string parameterName)
    {
        var currencyCode = SalaryPaymentMetadata.Normalize(value);
        if (currencyCode.Length == 0 || currencyCode.Length > 20 || currencyCode.Any(char.IsControl))
            throw new ArgumentException("An explicit currency code of at most 20 characters is required for loan and advance requests.", parameterName);
        return currencyCode;
    }

    private static bool HasCurrencyAmountPrecision(decimal amount, int decimalPlaces)
    {
        var scale = 1m;
        for (var i = 0; i < decimalPlaces; i++) scale *= 10m;
        return amount % (1m / scale) == 0m;
    }

    private async Task<(int DecimalPlaces, int? RoundingRule)> EnsureCurrencyAmountPrecisionAsync(
        string employeeCode,
        string? currencyCode,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = RequireCurrencyCode(currencyCode, nameof(currencyCode));
        var currencyRules = await _loanRepository.GetCurrencyRulesForEmployeeAsync(
            employeeCode, normalizedCurrency, cancellationToken);
        if (!HasCurrencyAmountPrecision(amount, currencyRules.DecimalPlaces))
            throw new ArgumentException(
                $"Amount exceeds the configured {currencyRules.DecimalPlaces}-decimal precision for currency '{normalizedCurrency}'.",
                nameof(amount));
        return currencyRules;
    }

    public async Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.LoanType))
            throw new ArgumentException("Employee and loan type are required.");
        if (dto.PrincipalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.PrincipalAmount), "Loan principal must be greater than zero.");
        if (dto.TotalInstallments <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.TotalInstallments), "Loan must have at least one installment.");
        if (dto.StartDate == default)
            throw new ArgumentException("Loan start date is required.", nameof(dto.StartDate));
        var normalizedCurrencyCode = RequireCurrencyCode(dto.CurrencyCode, nameof(dto.CurrencyCode));
        var currencyRules = await EnsureCurrencyAmountPrecisionAsync(
            dto.EmployeeCode, normalizedCurrencyCode, dto.PrincipalAmount, cancellationToken);
        var minorUnit = 1m;
        for (var i = 0; i < currencyRules.DecimalPlaces; i++)
            minorUnit /= 10m;
        if (dto.PrincipalAmount < minorUnit * dto.TotalInstallments)
            throw new ArgumentOutOfRangeException(nameof(dto.TotalInstallments),
                "Loan principal must cover at least one currency minor unit per installment.");

        var unroundedInstallment = dto.PrincipalAmount / dto.TotalInstallments;
        var installmentAmount = PayrollCurrencyRules.Round(
            unroundedInstallment, currencyRules.DecimalPlaces, currencyRules.RoundingRule);
        var finalInstallment = dto.PrincipalAmount - installmentAmount * (dto.TotalInstallments - 1);
        if (finalInstallment < minorUnit)
        {
            // A configured upward rounding rule can over-allocate early installments.
            // Fall back to a currency-precision floor so the final installment stays positive.
            installmentAmount = PayrollCurrencyRules.Round(unroundedInstallment, currencyRules.DecimalPlaces, 4);
            finalInstallment = dto.PrincipalAmount - installmentAmount * (dto.TotalInstallments - 1);
        }
        if (installmentAmount < minorUnit || finalInstallment < minorUnit ||
            !HasCurrencyAmountPrecision(installmentAmount, currencyRules.DecimalPlaces) ||
            !HasCurrencyAmountPrecision(finalInstallment, currencyRules.DecimalPlaces))
            throw new InvalidOperationException("Loan installments cannot be distributed as positive amounts at the configured currency precision.");

        var loan = new EmployeeLoan
        {
            EmployeeCode = dto.EmployeeCode,
            LoanType = dto.LoanType,
            PrincipalAmount = dto.PrincipalAmount,
            CurrencyCode = normalizedCurrencyCode,
            MonthlyInstallmentAmount = installmentAmount,
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
                ? finalInstallment
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

        async Task<EmployeeLoan> PersistAsync()
        {
            await _loanRepository.AddLoanAsync(loan, cancellationToken);
            await _loanRepository.SaveChangesAsync(cancellationToken);
            return loan;
        }

        return _unitOfWork == null
            ? await PersistAsync()
            : await _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, dto.StartDate,
                dto.StartDate.AddMonths(dto.TotalInstallments), PersistAsync, cancellationToken);
    }

    public async Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default)
    {
        var loan = await _loanRepository.GetLoanByIdAsync(loanId, cancellationToken);
        if (loan == null) throw new KeyNotFoundException($"Loan with ID {loanId} not found.");
        if (loan.Status == "APPROVED")
        {
            if (string.IsNullOrWhiteSpace(loan.CurrencyCode))
                throw new InvalidOperationException("An explicit currency is required before approving or funding this loan. Cancel and resubmit the request with its currency.");
            await _loanRepository.GetCurrencyRulesForEmployeeAsync(loan.EmployeeCode,
                SalaryPaymentMetadata.Normalize(loan.CurrencyCode), cancellationToken);
            return loan;
        }
        if (loan.Status != "PENDING") throw new InvalidOperationException("Only a pending loan request can be approved by HR.");
        var loanEmployeeUser = _employeeRepository == null ? null :
            await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(loan.EmployeeCode, cancellationToken);
        HrApprovalGuard.EnsureNotSelfApproved(user, loan.CreationUser, loanEmployeeUser, "loan request");
        if (string.IsNullOrWhiteSpace(loan.CurrencyCode))
            throw new InvalidOperationException("An explicit currency is required before approving or funding this loan. Cancel and resubmit the request with its currency.");
        await EnsureCurrencyAmountPrecisionAsync(loan.EmployeeCode, loan.CurrencyCode, loan.PrincipalAmount, cancellationToken);

        loan.Status = "APPROVED";
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
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            throw new ArgumentException("Employee is required.", nameof(dto.EmployeeCode));
        if (dto.AdvanceAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.AdvanceAmount), "Advance amount must be greater than zero.");
        var targetStart = ParsePayPeriod(dto.TargetPayPeriod);
        var normalizedCurrencyCode = RequireCurrencyCode(dto.CurrencyCode, nameof(dto.CurrencyCode));
        await EnsureCurrencyAmountPrecisionAsync(dto.EmployeeCode, normalizedCurrencyCode, dto.AdvanceAmount, cancellationToken);
        var advance = new EmployeeAdvance
        {
            EmployeeCode = dto.EmployeeCode,
            AdvanceAmount = dto.AdvanceAmount,
            CurrencyCode = normalizedCurrencyCode,
            TargetPayPeriod = dto.TargetPayPeriod,
            DeductedAmount = 0m,
            RemainingBalance = dto.AdvanceAmount,
            Status = "PENDING",
            Reason = dto.Reason,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        async Task<EmployeeAdvance> PersistAsync()
        {
            await _loanRepository.AddAdvanceAsync(advance, cancellationToken);
            await _loanRepository.SaveChangesAsync(cancellationToken);
            return advance;
        }

        return _unitOfWork == null
            ? await PersistAsync()
            : await _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, targetStart,
                targetStart.AddMonths(1).AddDays(-1), PersistAsync, cancellationToken);
    }

    private static DateTime ParsePayPeriod(string payPeriod)
    {
        if (!DateTime.TryParseExact(payPeriod, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var start))
            throw new ArgumentException("Target pay period must use yyyy-MM format.", nameof(payPeriod));
        return new DateTime(start.Year, start.Month, 1);
    }

    public async Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default)
    {
        var advance = await _loanRepository.GetAdvanceByIdAsync(advanceId, cancellationToken);
        if (advance == null) throw new KeyNotFoundException($"Advance with ID {advanceId} not found.");
        if (advance.Status == "APPROVED")
        {
            if (string.IsNullOrWhiteSpace(advance.CurrencyCode))
                throw new InvalidOperationException("An explicit currency is required before approving or funding this advance. Cancel and resubmit the request with its currency.");
            await _loanRepository.GetCurrencyRulesForEmployeeAsync(advance.EmployeeCode,
                SalaryPaymentMetadata.Normalize(advance.CurrencyCode), cancellationToken);
            return advance;
        }
        if (advance.Status != "PENDING") throw new InvalidOperationException("Only a pending advance request can be approved by HR.");
        var advanceEmployeeUser = _employeeRepository == null ? null :
            await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(advance.EmployeeCode, cancellationToken);
        HrApprovalGuard.EnsureNotSelfApproved(user, advance.CreationUser, advanceEmployeeUser, "advance request");
        if (string.IsNullOrWhiteSpace(advance.CurrencyCode))
            throw new InvalidOperationException("An explicit currency is required before approving or funding this advance. Cancel and resubmit the request with its currency.");
        await EnsureCurrencyAmountPrecisionAsync(advance.EmployeeCode, advance.CurrencyCode, advance.AdvanceAmount, cancellationToken);

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

    public async Task<LoanRepaymentSchedule> SkipInstallmentAsync(long scheduleId, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to skip/defer an installment.", nameof(reason));

        var schedule = await _loanRepository.GetScheduleByIdAsync(scheduleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan repayment schedule {scheduleId} not found.");

        if (schedule.Status != "PENDING")
            throw new InvalidOperationException($"Cannot skip installment in status '{schedule.Status}'. Only PENDING installments can be skipped.");

        if (schedule.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot skip an installment that is currently reserved by a payroll handoff.");

        var loan = schedule.EmployeeLoan
            ?? await _loanRepository.GetLoanByIdAsync(schedule.EmployeeLoanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parent loan {schedule.EmployeeLoanId} not found.");

        if (loan.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot skip installments for loan in status '{loan.Status}'. Only ACTIVE loans can have installments skipped.");

        schedule.Status = "SKIPPED";

        var lastSchedule = loan.Schedules.OrderBy(s => s.InstallmentNo).Last();
        var lastDate = DateTime.TryParseExact(lastSchedule.PayPeriod + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : DateTime.UtcNow;

        var nextPeriodCode = lastDate.AddMonths(1).ToString("yyyy-MM");
        var newInstallmentNo = lastSchedule.InstallmentNo + 1;
        var remainingUnpaid = schedule.ScheduledAmount - schedule.PaidAmount;

        var newSchedule = new LoanRepaymentSchedule
        {
            EmployeeLoanId = loan.Id,
            InstallmentNo = newInstallmentNo,
            PayPeriod = nextPeriodCode,
            ScheduledAmount = remainingUnpaid,
            PaidAmount = 0m,
            CarriedForwardAmount = 0m,
            Status = "PENDING"
        };

        loan.Schedules.Add(newSchedule);
        loan.TotalInstallments = loan.Schedules.Count(s => s.Status != "CANCELLED");
        loan.EndDate = lastDate.AddMonths(2);
        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[SKIPPED: Installment {schedule.InstallmentNo} deferred to {nextPeriodCode}. Reason: {reason}]"
            : $"{loan.Notes}; [SKIPPED: Installment {schedule.InstallmentNo} deferred to {nextPeriodCode}. Reason: {reason}]";
        loan.UpdateUser = user;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    public async Task<EmployeeLoan> CancelLoanAsync(long loanId, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to cancel a loan.", nameof(reason));

        var loan = await _loanRepository.GetLoanByIdAsync(loanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan {loanId} not found.");

        if (loan.Status == "CANCELLED")
            throw new InvalidOperationException("Loan is already cancelled.");

        if (loan.Status is "ACTIVE" or "SETTLED")
            throw new InvalidOperationException($"Cannot cancel loan in status '{loan.Status}'. Active or funded loans must be reversed via Accounting or settled early.");

        if (loan.TotalPaidAmount > 0)
            throw new InvalidOperationException("Cannot cancel a loan that has recorded payments.");

        if (loan.Schedules.Any(s => s.ReservedByHandoffId != null))
            throw new InvalidOperationException("Cannot cancel a loan while installments are reserved by a payroll handoff.");

        loan.Status = "CANCELLED";
        foreach (var s in loan.Schedules)
        {
            if (s.Status == "PENDING")
                s.Status = "CANCELLED";
        }

        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[CANCELLED: {reason}]"
            : $"{loan.Notes}; [CANCELLED: {reason}]";
        loan.UpdateUser = user;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task<EmployeeAdvance> CancelAdvanceAsync(long advanceId, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to cancel an advance.", nameof(reason));

        var advance = await _loanRepository.GetAdvanceByIdAsync(advanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Advance {advanceId} not found.");

        if (advance.Status == "CANCELLED")
            throw new InvalidOperationException("Advance is already cancelled.");

        if (advance.Status is "ACTIVE" or "RECOVERED")
            throw new InvalidOperationException($"Cannot cancel advance in status '{advance.Status}'. Active or funded advances must be reversed via Accounting or settled early.");

        if (advance.DeductedAmount > 0)
            throw new InvalidOperationException("Cannot cancel an advance that has recorded deductions.");

        if (advance.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot cancel an advance while it is reserved by a payroll handoff.");

        advance.Status = "CANCELLED";
        advance.Reason = string.IsNullOrWhiteSpace(advance.Reason)
            ? $"[CANCELLED: {reason}]"
            : $"{advance.Reason}; [CANCELLED: {reason}]";
        advance.UpdateUser = user;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return advance;
    }

    public async Task<EmployeeLoan> SettleLoanEarlyAsync(long loanId, SettleLoanEarlyDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.SettleAmount <= 0)
            throw new ArgumentException("Settle amount must be greater than zero.", nameof(dto.SettleAmount));

        var loan = await _loanRepository.GetLoanByIdAsync(loanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan {loanId} not found.");

        if (loan.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot settle early a loan in status '{loan.Status}'. Only ACTIVE loans can be settled.");

        await EnsureCurrencyAmountPrecisionAsync(loan.EmployeeCode, loan.CurrencyCode, dto.SettleAmount, cancellationToken);

        if (loan.Schedules.Any(s => s.ReservedByHandoffId != null))
            throw new InvalidOperationException("Cannot settle loan early while installments are reserved by an in-flight payroll handoff.");

        if (dto.SettleAmount > loan.RemainingBalance)
            throw new InvalidOperationException($"Settle amount {dto.SettleAmount} cannot exceed remaining loan balance {loan.RemainingBalance}.");

        loan.TotalPaidAmount += dto.SettleAmount;
        loan.RemainingBalance -= dto.SettleAmount;

        decimal remainingToApply = dto.SettleAmount;
        foreach (var s in loan.Schedules.Where(s => s.Status is "PENDING" or "PARTIAL" or "SKIPPED").OrderBy(s => s.InstallmentNo))
        {
            if (remainingToApply <= 0) break;
            decimal needed = s.ScheduledAmount - s.PaidAmount;
            if (remainingToApply >= needed)
            {
                s.PaidAmount = s.ScheduledAmount;
                s.Status = "PAID";
                s.PaidDate = dto.SettleDate ?? DateTime.UtcNow;
                remainingToApply -= needed;
            }
            else
            {
                s.PaidAmount += remainingToApply;
                s.Status = "PARTIAL";
                remainingToApply = 0;
            }
        }

        if (loan.RemainingBalance <= 0)
        {
            loan.Status = "SETTLED";
            loan.EndDate = dto.SettleDate ?? DateTime.UtcNow;
        }

        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[EARLY SETTLEMENT: {dto.SettleAmount} {loan.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]"
            : $"{loan.Notes}; [EARLY SETTLEMENT: {dto.SettleAmount} {loan.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]";
        loan.UpdateUser = user;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task<EmployeeAdvance> SettleAdvanceEarlyAsync(long advanceId, SettleAdvanceEarlyDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.SettleAmount <= 0)
            throw new ArgumentException("Settle amount must be greater than zero.", nameof(dto.SettleAmount));

        var advance = await _loanRepository.GetAdvanceByIdAsync(advanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Advance {advanceId} not found.");

        if (advance.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot settle early an advance in status '{advance.Status}'. Only ACTIVE advances can be settled.");

        await EnsureCurrencyAmountPrecisionAsync(advance.EmployeeCode, advance.CurrencyCode, dto.SettleAmount, cancellationToken);

        if (advance.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot settle advance early while it is reserved by an in-flight payroll handoff.");

        if (dto.SettleAmount > advance.RemainingBalance)
            throw new InvalidOperationException($"Settle amount {dto.SettleAmount} cannot exceed remaining advance balance {advance.RemainingBalance}.");

        advance.DeductedAmount += dto.SettleAmount;
        advance.RemainingBalance -= dto.SettleAmount;

        if (advance.RemainingBalance <= 0)
        {
            advance.Status = "RECOVERED";
        }

        advance.Reason = string.IsNullOrWhiteSpace(advance.Reason)
            ? $"[EARLY SETTLEMENT: {dto.SettleAmount} {advance.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]"
            : $"{advance.Reason}; [EARLY SETTLEMENT: {dto.SettleAmount} {advance.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]";
        advance.UpdateUser = user;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);
        return advance;
    }
}
