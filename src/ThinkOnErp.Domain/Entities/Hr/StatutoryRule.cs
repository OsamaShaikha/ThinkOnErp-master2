using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class StatutoryRule
{
    public long Id { get; set; }
    public string RuleType { get; set; } = string.Empty; // SSC_EMPLOYER_RATE, SSC_EMPLOYEE_RATE, SSC_HIGH_RISK_SURCHARGE, SSC_CEILING, INCOME_TAX_BRACKET, NATIONAL_CONTRIBUTION_THRESHOLD, NATIONAL_CONTRIBUTION_RATE, MINIMUM_WAGE, PERSONAL_EXEMPTION_SELF, PERSONAL_EXEMPTION_DEPENDENT
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
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
