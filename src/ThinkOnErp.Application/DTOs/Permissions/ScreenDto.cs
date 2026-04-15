namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for screen/page information.
/// </summary>
public class ScreenDto
{
    /// <summary>
    /// Unique identifier for the screen
    /// </summary>
    public Int64 ScreenId { get; set; }

    /// <summary>
    /// System ID this screen belongs to
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Parent screen ID for hierarchical screens
    /// </summary>
    public Int64? ParentScreenId { get; set; }

    /// <summary>
    /// Unique code identifier (e.g., 'invoices_list')
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic name of the screen
    /// </summary>
    public string ScreenNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English name of the screen
    /// </summary>
    public string ScreenNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Frontend route path
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Arabic description
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// English description
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Icon identifier
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Indicates if the screen is active
    /// </summary>
    public bool IsActive { get; set; }
}
