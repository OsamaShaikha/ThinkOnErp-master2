namespace ThinkOnErp.Application.DTOs.Branch;

/// <summary>
/// Data transfer object for updating an existing branch.
/// Used for PUT requests to update branch records.
/// </summary>
public class UpdateBranchDto
{
    /// <summary>
    /// Foreign key to SYS_COMPANY table - the parent company (optional)
    /// </summary>
    public decimal? ParRowId { get; set; }

    /// <summary>
    /// Arabic description of the branch (required)
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the branch (required)
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Branch phone number (optional)
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Branch mobile number (optional)
    /// </summary>
    public string? Mobile { get; set; }

    /// <summary>
    /// Branch fax number (optional)
    /// </summary>
    public string? Fax { get; set; }

    /// <summary>
    /// Branch email address (optional)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Indicates if this is the head/main branch of the company
    /// </summary>
    public bool IsHeadBranch { get; set; }
}
