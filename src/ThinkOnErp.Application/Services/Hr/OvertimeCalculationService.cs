using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IOvertimeCalculationService
{
    Task<decimal> CalculateOvertimeEarningsAsync(long companyId, decimal baseSalary, decimal overtimeHours, string dayType, DateTime date, CancellationToken cancellationToken = default);
    decimal DeriveHourlyWage(decimal baseSalary, int standardMonthlyHours = 240);
}

public sealed class OvertimeCalculationService : IOvertimeCalculationService
{
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

    public async Task<decimal> CalculateOvertimeEarningsAsync(
        long companyId,
        decimal baseSalary,
        decimal overtimeHours,
        string dayType,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        if (overtimeHours <= 0 || baseSalary <= 0)
            return 0.00m;

        var hourlyWage = DeriveHourlyWage(baseSalary);
        var rules = await _policyRepository.GetActiveOvertimeRulesAsync(companyId, date, cancellationToken);
        var matchedRule = rules.FirstOrDefault(r => r.DayType.Equals(dayType, StringComparison.OrdinalIgnoreCase));

        // Default multipliers if rule not explicitly configured
        decimal multiplier = matchedRule?.Multiplier ?? dayType.ToUpperInvariant() switch
        {
            "WEEKEND" => 1.50m,
            "HOLIDAY" => 2.00m,
            _ => 1.25m
        };

        var earnings = hourlyWage * overtimeHours * multiplier;
        return Math.Round(earnings, 3);
    }
}
