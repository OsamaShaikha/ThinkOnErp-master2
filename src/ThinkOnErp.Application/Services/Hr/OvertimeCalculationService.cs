using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IOvertimeCalculationService
{
    Task<decimal> CalculateOvertimeEarningsAsync(long companyId, decimal baseSalary, decimal overtimeHours, string dayType, DateTime date, CancellationToken cancellationToken = default);
    Task<decimal> CalculateOvertimeEarningsAsync(long companyId, long branchId, string? currencyCode, decimal baseSalary,
        decimal overtimeHours, string dayType, DateTime date, CancellationToken cancellationToken = default)
        => CalculateOvertimeEarningsAsync(companyId, baseSalary, overtimeHours, dayType, date, cancellationToken);
    decimal DeriveHourlyWage(decimal baseSalary, int standardMonthlyHours = 240);
}

public sealed class OvertimeCalculationService : IOvertimeCalculationService
{
    private static readonly Regex MonthlySalaryDivisorPattern = new(
        @"^\s*MONTHLY_SALARY\s*/\s*(?<divisor>[0-9]+(?:\.[0-9]+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IPolicyRepository _policyRepository;

    public OvertimeCalculationService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public decimal DeriveHourlyWage(decimal baseSalary, int standardMonthlyHours = 240)
    {
        if (standardMonthlyHours <= 0)
            standardMonthlyHours = 240;

        return Math.Round(baseSalary / standardMonthlyHours, 4);
    }

    public static bool TryParseMonthlyHoursDivisor(string? formula, out decimal divisor)
    {
        divisor = 0m;
        if (string.IsNullOrWhiteSpace(formula)) return false;
        var match = MonthlySalaryDivisorPattern.Match(formula);
        return match.Success
            && decimal.TryParse(match.Groups["divisor"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out divisor)
            && divisor > 0m;
    }

    public async Task<decimal> CalculateOvertimeEarningsAsync(
        long companyId,
        decimal baseSalary,
        decimal overtimeHours,
        string dayType,
        DateTime date,
        CancellationToken cancellationToken = default)
        => await CalculateOvertimeEarningsCoreAsync(companyId, baseSalary, overtimeHours, dayType, date,
            3, null, cancellationToken);

    public async Task<decimal> CalculateOvertimeEarningsAsync(
        long companyId,
        long branchId,
        string? currencyCode,
        decimal baseSalary,
        decimal overtimeHours,
        string dayType,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (branchId <= 0)
            throw new ArgumentOutOfRangeException(nameof(branchId));
        var configuredDecimalsByCurrency = await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(companyId, cancellationToken);
        var normalizedCurrency = SalaryPaymentMetadata.Normalize(currencyCode);
        if (normalizedCurrency.Length == 0)
            throw new InvalidOperationException("A payroll currency is required to calculate overtime earnings.");
        int? configuredDecimals = configuredDecimalsByCurrency.TryGetValue(normalizedCurrency, out var decimals) ? decimals : null;
        var currencyDecimals = PayrollCurrencyRules.GetCurrencyDecimals(currencyCode, configuredDecimals);
        var roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(branchId, cancellationToken) ?? 1;
        if (roundingRule is < 1 or > 6)
            throw new InvalidOperationException("Branch payroll rounding rule must be between 1 and 6.");
        return await CalculateOvertimeEarningsCoreAsync(companyId, baseSalary, overtimeHours, dayType, date,
            currencyDecimals, roundingRule, cancellationToken);
    }

    private async Task<decimal> CalculateOvertimeEarningsCoreAsync(
        long companyId,
        decimal baseSalary,
        decimal overtimeHours,
        string dayType,
        DateTime date,
        int currencyDecimals,
        int? roundingRule,
        CancellationToken cancellationToken)
    {
        if (overtimeHours <= 0 || baseSalary <= 0)
            return 0.00m;

        var rules = await _policyRepository.GetActiveOvertimeRulesAsync(companyId, date, cancellationToken);
        var normalizedDayType = (dayType ?? string.Empty).Trim();
        var matchedRule = rules?.FirstOrDefault(r => !string.IsNullOrEmpty(r.DayType) && r.DayType.Trim().Equals(normalizedDayType, StringComparison.OrdinalIgnoreCase));

        decimal hourlyDivisor = 240m;
        if (matchedRule != null)
        {
            if (!TryParseMonthlyHoursDivisor(matchedRule.HourlyDivisorFormula, out hourlyDivisor))
                throw new InvalidOperationException($"Overtime rule '{matchedRule.Code}' has an unsupported hourly divisor formula.");
            if (matchedRule.MinimumMinutes < 0 || matchedRule.MaximumMinutes <= 0 || matchedRule.MaximumMinutes < matchedRule.MinimumMinutes)
                throw new InvalidOperationException($"Overtime rule '{matchedRule.Code}' has an invalid minute range.");
            if (matchedRule.Multiplier <= 0m)
                throw new InvalidOperationException($"Overtime rule '{matchedRule.Code}' must have a positive multiplier.");

            if (overtimeHours * 60m < matchedRule.MinimumMinutes)
                return 0.00m;
            overtimeHours = Math.Min(overtimeHours, matchedRule.MaximumMinutes / 60m);
        }

        // Keep the derived hourly wage unrounded here. The final monetary amount is
        // rounded once below using the configured currency precision and branch rule.
        var hourlyWage = baseSalary / hourlyDivisor;

        // Default multipliers if rule not explicitly configured
        decimal multiplier = matchedRule?.Multiplier ?? normalizedDayType.ToUpperInvariant() switch
        {
            "WEEKEND" => 1.50m,
            "HOLIDAY" => 2.00m,
            _ => 1.25m
        };

        var earnings = hourlyWage * overtimeHours * multiplier;
        return PayrollCurrencyRules.Round(earnings, currencyDecimals, roundingRule);
    }
}
