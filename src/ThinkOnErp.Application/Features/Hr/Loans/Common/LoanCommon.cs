using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Common;

public static class LoanCommon
{
    public static T CopyScalars<T>(T source) where T : class, new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties().Where(p => p.CanRead && p.CanWrite &&
            (p.PropertyType.IsValueType || p.PropertyType == typeof(string))))
            property.SetValue(copy, property.GetValue(source));
        return copy;
    }

    public static string RequireCurrencyCode(string? value, string parameterName)
    {
        var currencyCode = SalaryPaymentMetadata.Normalize(value);
        if (currencyCode.Length == 0 || currencyCode.Length > 20 || currencyCode.Any(char.IsControl))
            throw new ArgumentException("An explicit currency code of at most 20 characters is required for loan and advance requests.", parameterName);
        return currencyCode;
    }

    public static bool HasCurrencyAmountPrecision(decimal amount, int decimalPlaces)
    {
        var scale = 1m;
        for (var i = 0; i < decimalPlaces; i++) scale *= 10m;
        return amount % (1m / scale) == 0m;
    }

    public static async Task<(int DecimalPlaces, int? RoundingRule)> EnsureCurrencyAmountPrecisionAsync(
        ILoanRepository loanRepository,
        string employeeCode,
        string? currencyCode,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = RequireCurrencyCode(currencyCode, nameof(currencyCode));
        var currencyRules = await loanRepository.GetCurrencyRulesForEmployeeAsync(
            employeeCode, normalizedCurrency, cancellationToken);
        if (!HasCurrencyAmountPrecision(amount, currencyRules.DecimalPlaces))
            throw new ArgumentException(
                $"Amount exceeds the configured {currencyRules.DecimalPlaces}-decimal precision for currency '{normalizedCurrency}'.",
                nameof(amount));
        return currencyRules;
    }

    public static DateTime ParsePayPeriod(string payPeriod)
    {
        if (!DateTime.TryParseExact(payPeriod, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start))
            throw new ArgumentException("Target pay period must use yyyy-MM format.", nameof(payPeriod));
        return new DateTime(start.Year, start.Month, 1);
    }

    public static async Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsCoreAsync(
        ILoanRepository loanRepository,
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
        var allSchedules = await loanRepository.GetPendingSchedulesForPeriodAsync(payPeriod, cancellationToken);
        var empSchedules = allSchedules.Where(s => s.EmployeeLoan is { Status: "ACTIVE" } loan && loan.EmployeeCode == employeeCode &&
            (s.Status == "PENDING" || s.Status == "PARTIAL" || s.Status == "SKIPPED") && s.ReservedByHandoffId == null)
            .OrderBy(s => s.PayPeriod).ThenBy(s => s.InstallmentNo).ToList();

        var allAdvances = await loanRepository.GetAdvancesAsync(employeeCode, payPeriod, cancellationToken);
        var empAdvances = allAdvances.Where(a => a.Status == "ACTIVE" && a.RemainingBalance > 0 && a.ReservedByHandoffId == null).ToList();

        (int DecimalPlaces, int? RoundingRule)? currencyRules = null;
        if (empSchedules.Count > 0 || empAdvances.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(payrollCurrencyCode))
                throw new InvalidOperationException("Payroll currency is required when active loan or advance deductions are present.");
            var normalizedPayrollCurrency = PayrollCurrencyRules.RequireSingleCurrency([payrollCurrencyCode])
                ?? throw new InvalidOperationException("Payroll currency is required when active loan or advance deductions are present.");
            currencyRules = await loanRepository.GetCurrencyRulesForEmployeeAsync(
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
}
