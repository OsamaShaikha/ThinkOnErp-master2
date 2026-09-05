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

public sealed class StatutoryRuleService : IStatutoryRuleService
{
    private readonly IStatutoryRuleRepository _repository;
    private readonly ILogger<StatutoryRuleService> _logger;

    public StatutoryRuleService(
        IStatutoryRuleRepository repository,
        ILogger<StatutoryRuleService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<StatutoryRuleDto>> GetAllRulesAsync(string? ruleType = null, bool activeOnly = true)
    {
        var rules = await _repository.GetAllAsync(ruleType, activeOnly);
        return rules.Select(MapToDto).ToList();
    }

    public async Task<StatutoryRuleDto?> GetRuleByIdAsync(long id)
    {
        var rule = await _repository.GetByIdAsync(id);
        return rule == null ? null : MapToDto(rule);
    }

    public async Task<decimal> GetEffectiveRateAsync(string ruleType, DateTime effectiveDate)
    {
        var rule = await _repository.GetEffectiveRuleAsync(ruleType, effectiveDate);
        if (rule == null || !rule.RatePercent.HasValue)
        {
            _logger.LogWarning("No active rate found for statutory rule type {RuleType} on date {EffectiveDate}", ruleType, effectiveDate);
            return 0m;
        }
        return rule.RatePercent.Value;
    }

    public async Task<decimal> GetEffectiveValueAsync(string ruleType, DateTime effectiveDate)
    {
        var rule = await _repository.GetEffectiveRuleAsync(ruleType, effectiveDate);
        if (rule == null || !rule.Value.HasValue)
        {
            _logger.LogWarning("No active value found for statutory rule type {RuleType} on date {EffectiveDate}", ruleType, effectiveDate);
            return 0m;
        }
        return rule.Value.Value;
    }

    public async Task<List<StatutoryRuleDto>> GetEffectiveTaxBracketsAsync(DateTime effectiveDate)
    {
        var brackets = await _repository.GetEffectiveRulesByTypeAsync("INCOME_TAX_BRACKET", effectiveDate);
        return brackets.Select(MapToDto).ToList();
    }

    public async Task<StatutoryRuleDto> CreateRuleAsync(CreateStatutoryRuleDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rule = new StatutoryRule
        {
            RuleType = dto.RuleType.Trim().ToUpperInvariant(),
            RuleName = dto.RuleName.Trim(),
            BracketLow = dto.BracketLow,
            BracketHigh = dto.BracketHigh,
            RatePercent = dto.RatePercent,
            Value = dto.Value,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "JOD" : dto.CurrencyCode.Trim().ToUpperInvariant(),
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            Description = dto.Description,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddAsync(rule);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Created statutory rule {RuleType} ({RuleName}) by {User}", rule.RuleType, rule.RuleName, currentUser);
        return MapToDto(rule);
    }

    public async Task<StatutoryRuleDto> UpdateRuleAsync(long id, UpdateStatutoryRuleDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rule = await _repository.GetByIdAsync(id);
        if (rule == null)
        {
            throw new HrNotFoundException($"القاعدة القانونية رقم ({id}) غير موجودة.", "STATUTORY_RULE_NOT_FOUND");
        }

        rule.RuleName = dto.RuleName.Trim();
        rule.BracketLow = dto.BracketLow;
        rule.BracketHigh = dto.BracketHigh;
        rule.RatePercent = dto.RatePercent;
        rule.Value = dto.Value;
        rule.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "JOD" : dto.CurrencyCode.Trim().ToUpperInvariant();
        rule.EffectiveFrom = dto.EffectiveFrom;
        rule.EffectiveTo = dto.EffectiveTo;
        rule.Description = dto.Description;
        rule.IsActive = dto.IsActive;
        rule.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        rule.UpdateDate = DateTime.UtcNow;

        _repository.Update(rule);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Updated statutory rule {Id} ({RuleName}) by {User}", id, rule.RuleName, currentUser);
        return MapToDto(rule);
    }

    public async Task<bool> DeleteRuleAsync(long id)
    {
        var rule = await _repository.GetByIdAsync(id);
        if (rule == null)
        {
            return false;
        }

        _repository.Remove(rule);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Deleted statutory rule {Id}", id);
        return true;
    }

    private static StatutoryRuleDto MapToDto(StatutoryRule r)
    {
        return new StatutoryRuleDto
        {
            Id = r.Id,
            RuleType = r.RuleType,
            RuleName = r.RuleName,
            BracketLow = r.BracketLow,
            BracketHigh = r.BracketHigh,
            RatePercent = r.RatePercent,
            Value = r.Value,
            CurrencyCode = r.CurrencyCode,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            Description = r.Description,
            IsActive = r.IsActive
        };
    }
}
