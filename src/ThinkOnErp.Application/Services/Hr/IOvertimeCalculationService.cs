using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IOvertimeCalculationService
{
    Task<decimal> CalculateOvertimeEarningsAsync(string employeeCode, decimal basicSalary, DateTime fromDate, DateTime toDate, long companyId);
    Task<List<OvertimeRuleDto>> GetOvertimeRulesAsync(long companyId, DateTime calculationDate);
    Task<OvertimeRuleDto> CreateOvertimeRuleAsync(CreateOvertimeRuleDto dto, string currentUser);
    Task<decimal> GetHourlyRateAsync(string employeeCode, decimal basicSalary, long companyId, DateTime calculationDate);
}
