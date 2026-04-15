namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents system access control per company (allow/block systems for companies).
/// Maps to the SYS_COMPANY_SYSTEM table in Oracle database.
/// </summary>
public class SysCompanySystem
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_COMPANY_SYSTEM sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_COMPANY table
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SYSTEM table
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Indicates if the system is allowed for this company (true=allowed, false=blocked)
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Foreign key to SYS_SUPER_ADMIN who granted/revoked access (nullable)
    /// </summary>
    public Int64? GrantedBy { get; set; }

    /// <summary>
    /// Timestamp when access was granted
    /// </summary>
    public DateTime? GrantedDate { get; set; }

    /// <summary>
    /// Timestamp when access was revoked (nullable)
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// Optional notes about the system assignment
    /// </summary>
    public string? Notes { get; set; }

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
