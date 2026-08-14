namespace ThinkOnErp.Domain.Entities.Accounting;

public class GlAccount
{
    public string AccountCode { get; set; } = string.Empty;
    public string? OldAccountCode { get; set; }
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }
    public int AccountLevel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }
    public string? ControlAccountType { get; set; }
    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPostable => AccountType == "DETAIL" && IsActive;
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public GlAccount? ParentAccount { get; set; }
    public ICollection<GlAccount> ChildrenAccounts { get; set; } = new List<GlAccount>();
    public ICollection<GlAccountBranch> BranchLinks { get; set; } = new List<GlAccountBranch>();
}
