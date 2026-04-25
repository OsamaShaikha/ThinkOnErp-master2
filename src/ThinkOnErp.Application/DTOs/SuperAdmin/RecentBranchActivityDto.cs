namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for recent branch activities
/// Validates Requirements: 8.3, 8.4, 8.5, 8.6
/// </summary>
public class RecentBranchActivityDto
{
    /// <summary>
    /// Branch name in Arabic
    /// </summary>
    public string BranchNameAr { get; set; }

    /// <summary>
    /// Branch name in English
    /// </summary>
    public string BranchNameEn { get; set; }

    /// <summary>
    /// Company name in Arabic
    /// </summary>
    public string CompanyNameAr { get; set; }

    /// <summary>
    /// Company name in English
    /// </summary>
    public string CompanyNameEn { get; set; }

    /// <summary>
    /// Activity type: "New" or "Update"
    /// </summary>
    public string ActivityType { get; set; }

    /// <summary>
    /// Date when the activity occurred
    /// </summary>
    public DateTime ActivityDate { get; set; }
}
