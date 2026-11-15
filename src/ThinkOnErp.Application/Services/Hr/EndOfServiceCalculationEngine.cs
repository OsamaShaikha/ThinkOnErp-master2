using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEndOfServiceCalculationEngine
{
    EndOfServiceSettlement Calculate(
        Employee employee,
        SalaryStructure salaryStructure,
        EndOfServicePolicy policy,
        string terminationType,
        DateTime lastWorkingDate,
        DateTime? noticeDate = null,
        int noticeCompensationDays = 0,
        decimal otherEntitlements = 0m,
        decimal otherDeductions = 0m,
        decimal remainingLeaveDays = 0m,
        decimal outstandingLoans = 0m,
        decimal outstandingAdvances = 0m,
        string? calculationNotes = null,
        int? currencyDecimals = null,
        int? branchRoundingRule = 1);
}

public sealed class EndOfServiceCalculationEngine : IEndOfServiceCalculationEngine
{
    public EndOfServiceSettlement Calculate(
        Employee employee,
        SalaryStructure salaryStructure,
        EndOfServicePolicy policy,
        string terminationType,
        DateTime lastWorkingDate,
        DateTime? noticeDate = null,
        int noticeCompensationDays = 0,
        decimal otherEntitlements = 0m,
        decimal otherDeductions = 0m,
        decimal remainingLeaveDays = 0m,
        decimal outstandingLoans = 0m,
        decimal outstandingAdvances = 0m,
        string? calculationNotes = null,
        int? currencyDecimals = null,
        int? branchRoundingRule = 1)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(salaryStructure);
        ArgumentNullException.ThrowIfNull(policy);

        if (!EndOfServiceTerminationTypes.All.Contains(terminationType))
            throw new ArgumentException($"Invalid termination type: '{terminationType}'.", nameof(terminationType));

        if (lastWorkingDate < employee.HireDate)
            throw new ArgumentException("Last working date cannot be earlier than hire date.", nameof(lastWorkingDate));

        var decimals = PayrollCurrencyRules.GetCurrencyDecimals(salaryStructure.CurrencyCode, currencyDecimals);
        if (branchRoundingRule is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(branchRoundingRule), "Branch payroll rounding rule must be between 1 and 6.");
        decimal RoundMoney(decimal amount) => PayrollCurrencyRules.Round(amount, decimals, branchRoundingRule);

        // 1. Service period calculation
        var (years, months, days) = CalculateServicePeriod(employee.HireDate, lastWorkingDate);

        // 2. Salary basis determination
        decimal basicSalary = salaryStructure.BasicSalary;
        decimal grossSalary = basicSalary;
        foreach (var line in salaryStructure.Lines)
        {
            if (line.IsActive && line.Component?.ComponentType == "ALLOWANCE")
                grossSalary += line.Amount;
        }

        decimal salaryBasis = policy.CalculationBasis == "GROSS_SALARY" ? grossSalary : basicSalary;
        decimal dailyRate = salaryBasis / (policy.LeaveDailyRateDivisor > 0 ? policy.LeaveDailyRateDivisor : 30m);

        // 3. Severance Pay Calculation
        decimal severancePay = CalculateSeverancePay(years, salaryBasis, terminationType, policy, RoundMoney);

        // 4. Unpaid salary for final period
        decimal unpaidSalary = CalculateUnpaidSalaryForFinalMonth(lastWorkingDate, grossSalary, RoundMoney);

        // 5. Leave encashment / negative leave deduction
        decimal leaveEncashment = 0m;
        decimal negativeLeaveDeduction = 0m;
        if (remainingLeaveDays > 0)
        {
            leaveEncashment = RoundMoney(remainingLeaveDays * dailyRate);
        }
        else if (remainingLeaveDays < 0)
        {
            negativeLeaveDeduction = RoundMoney(Math.Abs(remainingLeaveDays) * dailyRate);
        }

        // 6. Notice period pay
        decimal noticePeriodPay = 0m;
        if (noticeCompensationDays > 0)
        {
            noticePeriodPay = RoundMoney(noticeCompensationDays * dailyRate);
        }

        // 7. Aggregate Entitlements and Deductions
        otherEntitlements = RoundMoney(otherEntitlements);
        otherDeductions = RoundMoney(otherDeductions);
        outstandingLoans = RoundMoney(outstandingLoans);
        outstandingAdvances = RoundMoney(outstandingAdvances);
        decimal totalEntitlements = RoundMoney(severancePay + unpaidSalary + leaveEncashment + noticePeriodPay + otherEntitlements);
        decimal totalDeductions = RoundMoney(outstandingLoans + outstandingAdvances + negativeLeaveDeduction + otherDeductions);
        decimal netSettlement = RoundMoney(totalEntitlements - totalDeductions);

        var settlement = new EndOfServiceSettlement
        {
            CompanyId = policy.CompanyId,
            BranchId = employee.BranchId,
            EmployeeCode = employee.EmployeeCode,
            TerminationType = terminationType,
            TerminationReason = calculationNotes,
            NoticeDate = noticeDate,
            LastWorkingDate = lastWorkingDate,
            SettlementDate = DateTime.UtcNow,
            HireDate = employee.HireDate,
            ServiceYears = years,
            ServiceMonths = months,
            ServiceDays = days,
            BaseSalarySnapshot = basicSalary,
            GrossSalarySnapshot = grossSalary,
            CurrencyCode = salaryStructure.CurrencyCode ?? "JOD",

            SeverancePay = severancePay,
            UnpaidSalary = unpaidSalary,
            LeaveEncashment = leaveEncashment,
            UnusedLeaveDays = remainingLeaveDays,
            NoticePeriodPay = noticePeriodPay,
            OtherEntitlements = otherEntitlements,
            TotalEntitlements = totalEntitlements,

            OutstandingLoans = outstandingLoans,
            OutstandingAdvances = outstandingAdvances,
            NegativeLeaveDeduction = negativeLeaveDeduction,
            OtherDeductions = otherDeductions,
            TotalDeductions = totalDeductions,

            NetSettlementAmount = netSettlement,
            Status = EndOfServiceSettlementStatus.Draft,
            CalculationNotes = calculationNotes
        };

        // Itemized Lines
        if (severancePay > 0)
            settlement.Lines.Add(new() { ComponentCode = "SEVERANCE", ComponentType = "ENTITLEMENT", Amount = severancePay, Description = $"Severance pay for {years:F2} years of service ({terminationType})" });
        if (unpaidSalary > 0)
            settlement.Lines.Add(new() { ComponentCode = "UNPAID_SALARY", ComponentType = "ENTITLEMENT", Amount = unpaidSalary, Description = $"Prorated salary for final month up to {lastWorkingDate:yyyy-MM-dd}" });
        if (leaveEncashment > 0)
            settlement.Lines.Add(new() { ComponentCode = "LEAVE_CASHOUT", ComponentType = "ENTITLEMENT", Amount = leaveEncashment, Description = $"Encashment of {remainingLeaveDays:F2} unused leave days" });
        if (noticePeriodPay > 0)
            settlement.Lines.Add(new() { ComponentCode = "NOTICE_PAY", ComponentType = "ENTITLEMENT", Amount = noticePeriodPay, Description = $"Compensation for {noticeCompensationDays} notice period days" });
        if (otherEntitlements > 0)
            settlement.Lines.Add(new() { ComponentCode = "OTHER_ENTITLEMENT", ComponentType = "ENTITLEMENT", Amount = otherEntitlements, Description = "Additional entitlements" });

        if (outstandingLoans > 0)
            settlement.Lines.Add(new() { ComponentCode = "LOAN_DEDUCTION", ComponentType = "DEDUCTION", Amount = outstandingLoans, Description = "Outstanding loan balance settlement" });
        if (outstandingAdvances > 0)
            settlement.Lines.Add(new() { ComponentCode = "ADVANCE_DEDUCTION", ComponentType = "DEDUCTION", Amount = outstandingAdvances, Description = "Outstanding advance balance settlement" });
        if (negativeLeaveDeduction > 0)
            settlement.Lines.Add(new() { ComponentCode = "NEGATIVE_LEAVE_DEDUCTION", ComponentType = "DEDUCTION", Amount = negativeLeaveDeduction, Description = $"Deduction for {Math.Abs(remainingLeaveDays):F2} excess leave days taken" });
        if (otherDeductions > 0)
            settlement.Lines.Add(new() { ComponentCode = "OTHER_DEDUCTION", ComponentType = "DEDUCTION", Amount = otherDeductions, Description = "Additional liabilities / equipment / custody deduction" });

        return settlement;
    }

    private static (decimal Years, int Months, int Days) CalculateServicePeriod(DateTime hireDate, DateTime lastWorkingDate)
    {
        int years = 0;
        var temp = hireDate.Date;
        while (temp.AddYears(1) <= lastWorkingDate.Date)
        {
            years++;
            temp = temp.AddYears(1);
        }

        int months = 0;
        while (temp.AddMonths(1) <= lastWorkingDate.Date)
        {
            months++;
            temp = temp.AddMonths(1);
        }

        int remainingDays = (lastWorkingDate.Date - temp).Days;

        decimal decimalYears = years + (months / 12m) + (remainingDays / 365m);
        return (Math.Round(decimalYears, 4), (years * 12) + months, remainingDays);
    }

    private static decimal CalculateSeverancePay(decimal serviceYears, decimal salaryBasis, string terminationType, EndOfServicePolicy policy, Func<decimal, decimal> roundMoney)
    {
        if (serviceYears <= 0) return 0m;
        if (string.Equals(terminationType, EndOfServiceTerminationTypes.JobAbandonment, StringComparison.OrdinalIgnoreCase))
            return 0m;

        // Base severance accumulation:
        // First 5 years at FirstFiveYearsRatePerYear (e.g. 0.5 month per year)
        // Subsequent years at SubsequentYearsRatePerYear (e.g. 1.0 month per year)
        decimal firstFiveYears = Math.Min(serviceYears, 5.0m);
        decimal subsequentYears = Math.Max(0.0m, serviceYears - 5.0m);

        decimal baseAccrualMonths = (firstFiveYears * policy.FirstFiveYearsRatePerYear) +
                                    (subsequentYears * policy.SubsequentYearsRatePerYear);

        decimal fullSeverance = baseAccrualMonths * salaryBasis;

        // Apply resignation factor if employee resigned
        if (string.Equals(terminationType, EndOfServiceTerminationTypes.Resignation, StringComparison.OrdinalIgnoreCase))
        {
            decimal resignationFactor;
            if (serviceYears < 2.0m)
                resignationFactor = policy.ResignationFactorUnderTwoYears;
            else if (serviceYears < 5.0m)
                resignationFactor = policy.ResignationFactorTwoToFiveYears;
            else if (serviceYears < 10.0m)
                resignationFactor = policy.ResignationFactorFiveToTenYears;
            else
                resignationFactor = policy.ResignationFactorTenYearsAndAbove;

            return roundMoney(fullSeverance * resignationFactor);
        }

        return roundMoney(fullSeverance);
    }

    private static decimal CalculateUnpaidSalaryForFinalMonth(DateTime lastWorkingDate, decimal grossSalary, Func<decimal, decimal> roundMoney)
    {
        int daysInMonth = DateTime.DaysInMonth(lastWorkingDate.Year, lastWorkingDate.Month);
        int workedDays = Math.Min(lastWorkingDate.Day, daysInMonth);

        // If worked full month, return full gross
        if (workedDays == daysInMonth)
            return roundMoney(grossSalary);

        decimal fraction = workedDays / (decimal)daysInMonth;
        return roundMoney(grossSalary * fraction);
    }
}
