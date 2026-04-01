namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a system role for authorization in the ERP system.
/// Maps to the SYS_ROLE table in Oracle database.
/// </summary>
public class SysRole
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_ROLE sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Arabic description of the role
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the role
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Note { get; set; }

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
