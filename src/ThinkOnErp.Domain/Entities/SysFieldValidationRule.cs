namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a metadata-driven field validation rule stored centrally in the master schema (THINKON_ERP).
/// Controls validation rules (Required, Regex, Length, Range) dynamically per entity, country, or company.
/// </summary>
public class SysFieldValidationRule
{
    public long Id { get; set; }
    
    /// <summary>
    /// Target entity/DTO name, e.g., 'Customer', 'GlAccount', 'GlVoucherDetail', 'TaxRate'
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Target property/field name, e.g., 'TaxNumber', 'AccountCode', 'CreditLimit', 'Debit'
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Type of validation rule: 'REQUIRED', 'REGEX', 'MIN_LENGTH', 'MAX_LENGTH', 'RANGE', 'CUSTOM'
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule parameters or pattern (e.g., regex pattern, length integer, range 'min..max')
    /// </summary>
    public string? RuleValue { get; set; }

    /// <summary>
    /// Error translation code linked to SYS_CODE (e.g. 'ERR_INVALID_TAX_NUMBER', 'ERR_FIELD_REQUIRED')
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional ISO country code ('SA', 'JO', 'EG', 'GB'). If NULL, rule applies globally to all countries.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Optional Company ID override. If NULL, rule applies globally to all companies.
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// 1 = Active, 0 = Inactive
    /// </summary>
    public int IsActive { get; set; } = 1;

    public string CreationUser { get; set; } = "system";
    public DateTime? CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
