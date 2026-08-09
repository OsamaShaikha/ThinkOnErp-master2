namespace ThinkOnErp.Application.DTOs.Accounting;

/// <summary>
/// Updates the editable attributes of an existing GL account.
/// Structural fields such as account code, parent, category, level, type,
/// and normal balance remain immutable after creation.
/// </summary>
public sealed class UpdateGlAccountDto
{
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsControlAccount { get; set; }

    /// <summary>AR, AP, or INVENTORY when IsControlAccount is true.</summary>
    public string? ControlAccountType { get; set; }

    public bool IsBranchSpecific { get; set; }
    public bool IsClearing { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Active branch assignments. Required when IsBranchSpecific is true;
    /// otherwise it must be empty.
    /// </summary>
    public List<long> BranchIds { get; set; } = new();
}
