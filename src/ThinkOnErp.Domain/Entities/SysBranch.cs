namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a branch location within a company in the ERP system.
/// Includes contact information and head branch designation.
/// Maps to the SYS_BRANCH table in Oracle database.
/// </summary>
public class SysBranch
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_BRANCH sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_COMPANY table - the parent company
    /// </summary>
    public Int64? ParRowId { get; set; }

    /// <summary>
    /// Arabic description of the branch
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the branch
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

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
    /// Soft delete flag - true for active, false for deleted
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
