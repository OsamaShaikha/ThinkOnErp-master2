namespace ThinkOnErp.Application.DTOs.Accounting;

/// <summary>
/// Represents a major category (Level 1) or sub-category (Level 2) account in the Chart of Accounts.
/// </summary>
public sealed class GlAccountCategoryDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int AccountLevel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }
    public bool IsActive { get; set; }
    public List<GlAccountCategoryDto> SubCategories { get; set; } = new();
}
