namespace ThinkOnErp.Application.DTOs.Accounting;

public class GlAccountDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string? OldAccountCode { get; set; }
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }
    public int CategoryCode { get; set; }
    public int AccountLevel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }
    public string? ControlAccountType { get; set; }
    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public bool IsActive { get; set; }
    public bool IsPostable { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<long> BranchIds { get; set; } = new();
}
