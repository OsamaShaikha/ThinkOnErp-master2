namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a fiscal year period for a company branch in the ERP system.
/// Tracks financial periods with start/end dates and closure status.
/// Maps to the SYS_FISCAL_YEAR table in Oracle database.
/// </summary>
public class SysFiscalYear
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_FISCAL_YEAR sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_COMPANY table - the company this fiscal year belongs to
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table - the branch this fiscal year belongs to
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Unique fiscal year code (e.g., 'FY2024', 'FY2025')
    /// Must be unique per branch
    /// </summary>
    public string FiscalYearCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the fiscal year
    /// </summary>
    public string? RowDesc { get; set; }

    /// <summary>
    /// English description of the fiscal year
    /// </summary>
    public string? RowDescE { get; set; }

    /// <summary>
    /// Start date of the fiscal year period
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the fiscal year period
    /// Must be after StartDate
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Indicates if the fiscal year is closed
    /// Closed fiscal years typically cannot be modified
    /// </summary>
    public bool IsClosed { get; set; }

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

    // Navigation properties
    /// <summary>
    /// Navigation property to the parent company
    /// </summary>
    public SysCompany? Company { get; set; }

    /// <summary>
    /// Navigation property to the parent branch
    /// </summary>
    public SysBranch? Branch { get; set; }
}
