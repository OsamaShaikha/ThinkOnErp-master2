using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IStatutoryRuleService
{
    Task<List<StatutoryRuleDto>> GetAllRulesAsync(string? ruleType = null, bool activeOnly = true);
    Task<StatutoryRuleDto?> GetRuleByIdAsync(long id);
    Task<decimal> GetEffectiveRateAsync(string ruleType, DateTime effectiveDate);
    Task<decimal> GetEffectiveValueAsync(string ruleType, DateTime effectiveDate);
    Task<List<StatutoryRuleDto>> GetEffectiveTaxBracketsAsync(DateTime effectiveDate);
    Task<StatutoryRuleDto> CreateRuleAsync(CreateStatutoryRuleDto dto, string currentUser);
    Task<StatutoryRuleDto> UpdateRuleAsync(long id, UpdateStatutoryRuleDto dto, string currentUser);
    Task<bool> DeleteRuleAsync(long id);
}
