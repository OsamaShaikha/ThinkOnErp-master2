namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class CoaImportRowDto
{
    public int RowNumber { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string? OldAccountCode { get; set; }
    public string? ParentCode { get; set; }
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int AccountLevel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public string FinancialStatement { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }
    public string? ControlAccountType { get; set; }
    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public string? Notes { get; set; }
}
