namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class CreateGlAccountDto
{
    public string? AccountCode { get; set; }
    public string? OldAccountCode { get; set; }
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }
    public string AccountType { get; set; } = "DETAIL";
    public string? NormalBalance { get; set; }
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }
    public string? ControlAccountType { get; set; }
    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<long> BranchIds { get; set; } = new();
}
