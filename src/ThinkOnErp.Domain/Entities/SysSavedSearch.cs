namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Domain entity representing a saved search configuration for tickets.
/// Allows users to save frequently used search criteria for quick access.
/// Requirements: 8.6, 8.11, 19.9
/// </summary>
public class SysSavedSearch
{
    /// <summary>
    /// Unique identifier for the saved search
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// User ID who owns this saved search
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Name of the saved search
    /// </summary>
    public string SearchName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this search does
    /// </summary>
    public string? SearchDescription { get; set; }

    /// <summary>
    /// JSON string containing the search criteria parameters
    /// </summary>
    public string SearchCriteria { get; set; } = string.Empty;

    /// <summary>
    /// Whether this search is shared with all users (Y) or private (N)
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Whether this is the default search for the user
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Number of times this search has been used
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Last time this search was used
    /// </summary>
    public DateTime? LastUsedDate { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// User who created this saved search
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Date when this saved search was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// User who last updated this saved search
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Date when this saved search was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysUser? User { get; set; }
}
