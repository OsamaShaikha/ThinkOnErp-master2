using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class OvertimeCalculationService : IOvertimeCalculationService
{
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly IWorkCalendarRepository _workCalendarRepo;
    private readonly ILogger<OvertimeCalculationService> _logger;

    public OvertimeCalculationService(
        IAttendanceRepository attendanceRepo,
        IPolicyRepository policyRepo,
        IWorkCalendarRepository workCalendarRepo,
        ILogger<OvertimeCalculationService> logger)
    {
        _attendanceRepo = attendanceRepo ?? throw new ArgumentNullException(nameof(attendanceRepo));
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _workCalendarRepo = workCalendarRepo ?? throw new ArgumentNullException(nameof(workCalendarRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> CalculateOvertimeEarningsAsync(string employeeCode, decimal basicSalary, DateTime fromDate, DateTime toDate, long companyId)
    {
        if (basicSalary <= 0) return 0m;

        // Fetch ONLY approved overtime for this employee within the period
        var approvedRecords = await _attendanceRepo.GetOvertimeAsync(
            employeeCode: employeeCode,
            fromDate: fromDate,
            toDate: toDate,
            status: "APPROVED");

        if (approvedRecords.Count == 0) return 0m;

        var totalEarnings = 0m;

        foreach (var record in approvedRecords)
        {
            var targetDate = record.OvertimeDate.Date;

            // Resolve day type dynamically (Holiday, Weekend, or Normal)
            var holiday = await _workCalendarRepo.GetHolidayByDateAsync(companyId, targetDate);
            var calendar = await _workCalendarRepo.GetDefaultCalendarAsync(companyId, targetDate);
            var dayConfig = calendar?.Days.FirstOrDefault(d => d.DayOfWeek == targetDate.DayOfWeek);
            var isWeekend = dayConfig != null ? !dayConfig.IsWorkingDay : (targetDate.DayOfWeek == DayOfWeek.Friday || targetDate.DayOfWeek == DayOfWeek.Saturday);

            string dayType = "NORMAL";
            if (holiday != null) dayType = "HOLIDAY";
            else if (isWeekend) dayType = "WEEKEND";

            // Resolve dynamic overtime rule for this company and day type
            var rule = await _policyRepo.GetEffectiveOvertimeRuleAsync(companyId, dayType, targetDate);
            var multiplier = rule?.Multiplier ?? record.RateMultiplier; // fallback to record multiplier if rule not found
            if (multiplier <= 0) multiplier = 1.25m;

            // Calculate hourly rate dynamically from formula
            var divisor = 240m;
            if (rule != null && rule.HourlyDivisorFormula == "ACTUAL_MONTHLY_HOURS")
            {
                var daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
                divisor = daysInMonth * 8m;
            }
            else if (rule != null && rule.HourlyDivisorFormula == "WORKING_DAYS_HOURS")
            {
                var workDaysCount = 22; // default standard
                if (calendar != null)
                {
                    var workingDaysInMonth = 0;
                    var daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var curDate = new DateTime(targetDate.Year, targetDate.Month, d);
                        var curDay = calendar.Days.FirstOrDefault(cd => cd.DayOfWeek == curDate.DayOfWeek);
                        if (curDay?.IsWorkingDay == true) workingDaysInMonth++;
                    }
                    if (workingDaysInMonth > 0) workDaysCount = workingDaysInMonth;
                }
                divisor = workDaysCount * 8m;
            }

            var hourlyRate = basicSalary / divisor;
            var recordEarning = record.Hours * multiplier * hourlyRate;
            totalEarnings += recordEarning;
        }

        return Math.Round(totalEarnings, 3);
    }

    public async Task<decimal> GetHourlyRateAsync(string employeeCode, decimal basicSalary, long companyId, DateTime calculationDate)
    {
        if (basicSalary <= 0) return 0m;

        var normalRule = await _policyRepo.GetEffectiveOvertimeRuleAsync(companyId, "NORMAL", calculationDate);
        var divisor = 240m;

        if (normalRule != null && normalRule.HourlyDivisorFormula == "ACTUAL_MONTHLY_HOURS")
        {
            divisor = DateTime.DaysInMonth(calculationDate.Year, calculationDate.Month) * 8m;
        }

        return Math.Round(basicSalary / divisor, 4);
    }

    public async Task<List<OvertimeRuleDto>> GetOvertimeRulesAsync(long companyId, DateTime calculationDate)
    {
        var rules = await _policyRepo.GetEffectiveOvertimeRulesAsync(companyId, calculationDate);
        return rules.Select(r => new OvertimeRuleDto
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            Code = r.Code,
            NameEn = r.NameEn,
            NameAr = r.NameAr,
            DayType = r.DayType,
            Multiplier = r.Multiplier,
            MinimumMinutes = r.MinimumMinutes,
            MaximumMinutes = r.MaximumMinutes,
            HourlyDivisorFormula = r.HourlyDivisorFormula,
            RequiresApproval = r.RequiresApproval,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            IsActive = r.IsActive
        }).ToList();
    }

    public async Task<OvertimeRuleDto> CreateOvertimeRuleAsync(CreateOvertimeRuleDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rule = new OvertimeRule
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            DayType = dto.DayType.ToUpperInvariant(),
            Multiplier = dto.Multiplier,
            MinimumMinutes = dto.MinimumMinutes,
            MaximumMinutes = dto.MaximumMinutes,
            HourlyDivisorFormula = dto.HourlyDivisorFormula,
            RequiresApproval = dto.RequiresApproval,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _policyRepo.AddOvertimeRuleAsync(rule);
        await _policyRepo.SaveChangesAsync();

        return new OvertimeRuleDto
        {
            Id = rule.Id,
            CompanyId = rule.CompanyId,
            Code = rule.Code,
            NameEn = rule.NameEn,
            NameAr = rule.NameAr,
            DayType = rule.DayType,
            Multiplier = rule.Multiplier,
            MinimumMinutes = rule.MinimumMinutes,
            MaximumMinutes = rule.MaximumMinutes,
            HourlyDivisorFormula = rule.HourlyDivisorFormula,
            RequiresApproval = rule.RequiresApproval,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive
        };
    }
}
