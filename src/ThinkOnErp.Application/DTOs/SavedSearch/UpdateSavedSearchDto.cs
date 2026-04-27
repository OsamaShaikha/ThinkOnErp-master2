namespace ThinkOnErp.Application.DTOs.SavedSearch;

/// <summary>
/// DTO for updating an existing saved search.
/// Requirements: 8.6, 8.11
/// </summary>
public class UpdateSavedSearchDto
{
    public string SearchName { get; set; } = string.Empty;
    public string? SearchDescription { get; set; }
    public string SearchCriteria { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsDefault { get; set; }
}
