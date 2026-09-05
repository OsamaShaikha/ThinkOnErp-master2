using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class StatutoryRuleDto
{
    public long Id { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public decimal? BracketLow { get; set; }
    public decimal? BracketHigh { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? Value { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateStatutoryRuleDto
{
    public string RuleType { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public decimal? BracketLow { get; set; }
    public decimal? BracketHigh { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? Value { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdateStatutoryRuleDto
{
    public string RuleName { get; set; } = string.Empty;
    public decimal? BracketLow { get; set; }
    public decimal? BracketHigh { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? Value { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
