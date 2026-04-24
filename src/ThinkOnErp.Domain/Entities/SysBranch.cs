namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a branch location within a company in the ERP system.
/// Includes contact information, head branch designation, and operational settings.
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
    /// Default language for the branch (ar/en)
    /// </summary>
    public string? DefaultLang { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table - base currency for branch operations
    /// </summary>
    public Int64? BaseCurrencyId { get; set; }

    /// <summary>
    /// Rounding rules for calculations (1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR)
    /// </summary>
    public int? RoundingRules { get; set; }

    /// <summary>
    /// Branch logo image stored as byte array (BLOB in database)
    /// </summary>
    public byte[]? BranchLogo { get; set; }

    /// <summary>
    /// Indicates if the branch has a logo (derived property)
    /// </summary>
    public bool HasLogo => BranchLogo != null && BranchLogo.Length > 0;

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
    /// Navigation property to the base currency
    /// </summary>
    public SysCurrency? BaseCurrency { get; set; }
}
