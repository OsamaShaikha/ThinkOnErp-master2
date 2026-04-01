using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.DTOs.Branch;

/// <summary>
/// Data transfer object for branch information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class BranchDto
{
    /// <summary>
    /// Unique identifier for the branch
    /// </summary> 
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Foreign key to SYS_COMPANY table - the parent company
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Arabic description of the branch
    /// </summary>
    public string BranchNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the branch
    /// </summary>
    public string BranchNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Branch phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Branch mobile number
    /// </summary>
    public string? Mobile { get; set; }

    /// <summary>
    /// Branch fax number
    /// </summary>
    public string? Fax { get; set; }

    /// <summary>
    /// Branch email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Indicates if this is the head/main branch of the company
    /// </summary>
    public bool IsHeadBranch { get; set; }

    /// <summary>
    /// Indicates if the branch is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }
}
