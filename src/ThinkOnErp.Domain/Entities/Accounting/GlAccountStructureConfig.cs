namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Defines the Chart of Accounts level digit structure configuration.
/// Dictates the number of digits added at each level of the account hierarchy.
/// </summary>
public class GlAccountStructureConfig
{
    /// <summary>
    /// Level number in the hierarchy (1, 2, 3, 4, 5, 6...).
    /// </summary>
    public int LevelNumber { get; set; }

    /// <summary>
    /// Number of digits allocated at this specific level.
    /// </summary>
    public int DigitLength { get; set; }

    /// <summary>
    /// Arabic name for the level (e.g., "المستوى الأول - الحساب الرئيسي").
    /// </summary>
    public string LevelNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English name for the level (e.g., "Level 1 - Main Account").
    /// </summary>
    public string LevelNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Optional description or guidelines for this level.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this level configuration is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
