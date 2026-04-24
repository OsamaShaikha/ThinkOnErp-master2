namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a company/organization entity in the ERP system.
/// Includes foreign keys to Country and Currency.
/// Maps to the SYS_COMPANY table in Oracle database.
/// </summary>
public class SysCompany
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_COMPANY sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Arabic description of the company
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the company
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Country table
    /// </summary>
    public Int64? CountryId { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table
    /// </summary>
    public Int64? CurrId { get; set; }

    /// <summary>
    /// Legal name of the company in Arabic
    /// </summary>
    public string? LegalName { get; set; }

    /// <summary>
    /// Legal name of the company in English
    /// </summary>
    public string? LegalNameE { get; set; }

    /// <summary>
    /// Unique company code for identification
    /// </summary>
    public string? CompanyCode { get; set; }

    /// <summary>
    /// Tax registration number
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Foreign key to SYS_FISCAL_YEAR table - current active fiscal year
    /// </summary>
    public Int64? FiscalYearId { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table - references the default/head branch for this company
    /// </summary>
    public Int64? DefaultBranchId { get; set; }

    /// <summary>
    /// Company logo image stored as byte array
    /// </summary>
    public byte[]? CompanyLogo { get; set; }

    /// <summary>
    /// Indicates if the company has a logo (derived property)
    /// </summary>
    public bool HasLogo => CompanyLogo != null && CompanyLogo.Length > 0;

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
    /// Navigation property to the current fiscal year
    /// </summary>
    public SysFiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// Navigation property to the default currency
    /// </summary>
    public SysCurrency? Currency { get; set; }

    /// <summary>
    /// Navigation property to the default branch
    /// </summary>
    public SysBranch? DefaultBranch { get; set; }
}
