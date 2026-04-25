namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for recently created companies
/// Validates Requirements: 7.3, 7.4, 7.5, 7.6, 7.7
/// </summary>
public class RecentCompanyDto
{
    /// <summary>
    /// Company name in Arabic
    /// </summary>
    public string NameAr { get; set; }

    /// <summary>
    /// Company name in English
    /// </summary>
    public string NameEn { get; set; }

    /// <summary>
    /// Country where the company is located (optional)
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Number of branches associated with this company
    /// </summary>
    public int BranchCount { get; set; }

    /// <summary>
    /// Company status: "Active" or "Inactive"
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Date when the company was created
    /// </summary>
    public DateTime CreatedDate { get; set; }
}
