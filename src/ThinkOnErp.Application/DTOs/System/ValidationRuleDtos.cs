namespace ThinkOnErp.Application.DTOs.System;

public record SysFieldValidationRuleDto
{
    public long Id { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string RuleType { get; init; } = string.Empty;
    public string? RuleValue { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string? CountryCode { get; init; }
    public long? CompanyId { get; init; }
    public int IsActive { get; init; } = 1;
    public string CreationUser { get; init; } = string.Empty;
    public DateTime? CreationDate { get; init; }
    public string? UpdateUser { get; init; }
    public DateTime? UpdateDate { get; init; }
}

public record CreateValidationRuleDto
{
    public string EntityName { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string RuleType { get; init; } = string.Empty; // REQUIRED, REGEX, MIN_LENGTH, MAX_LENGTH, RANGE, POSITIVE
    public string? RuleValue { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string? CountryCode { get; init; }
    public long? CompanyId { get; init; }
}

public record UpdateValidationRuleDto
{
    public string? RuleValue { get; init; }
    public string? ErrorCode { get; init; }
    public string? CountryCode { get; init; }
    public long? CompanyId { get; init; }
    public int? IsActive { get; init; }
}

public record ValidationRuleFilterDto
{
    public string? EntityName { get; init; }
    public string? CountryCode { get; init; }
    public long? CompanyId { get; init; }
    public int? IsActive { get; init; }
}
