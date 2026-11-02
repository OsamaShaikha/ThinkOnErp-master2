using System;

namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Unified System, Company, and Branch Settings Entity.
/// BranchId = 0 indicates Company-wide default setting.
/// BranchId > 0 indicates Branch-specific setting override.
/// </summary>
public class SysSetting
{
    /// <summary>
    /// Branch ID (0 = Company-level default, > 0 = Branch-specific override)
    /// </summary>
    public long BranchId { get; set; } = 0;

    /// <summary>
    /// Numeric Setting Code (e.g. 1-6 for file paths, 50-58 for POS/Inventory settings)
    /// </summary>
    public int SettingCode { get; set; }

    /// <summary>
    /// Programmatic key name (e.g. 'POS_DEFAULT_WAREHOUSE', 'POS_STOCK_DEDUCTION_MODE')
    /// </summary>
    public string? SettingKey { get; set; }

    /// <summary>
    /// Human-readable setting description
    /// </summary>
    public string SettingDesc { get; set; } = string.Empty;

    /// <summary>
    /// Setting value (numeric ID, code, path, or string representation)
    /// </summary>
    public string SettingValue { get; set; } = string.Empty;

    /// <summary>
    /// Module grouping ('GENERAL', 'POS', 'INVENTORY', 'ACCOUNTING')
    /// </summary>
    public string Module { get; set; } = "GENERAL";

    /// <summary>
    /// Data type ('STRING', 'NUMBER', 'BOOLEAN', 'JSON')
    /// </summary>
    public string DataType { get; set; } = "STRING";

    /// <summary>
    /// Active state flag
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Audit fields
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
