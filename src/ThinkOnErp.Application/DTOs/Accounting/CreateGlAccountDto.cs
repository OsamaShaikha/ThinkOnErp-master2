namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class CreateGlAccountDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public long? ParentAccountId { get; set; }
    public long CategoryId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }
    public string? ControlAccountType { get; set; }
    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<long> BranchIds { get; set; } = new();
}
