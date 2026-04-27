namespace ThinkOnErp.Application.DTOs.SavedSearch;

/// <summary>
/// DTO for saved search information.
/// Requirements: 8.6, 8.11
/// </summary>
public class SavedSearchDto
{
    public Int64 SavedSearchId { get; set; }
    public Int64 UserId { get; set; }
    public string? UserName { get; set; }
    public string SearchName { get; set; } = string.Empty;
    public string? SearchDescription { get; set; }
    public string SearchCriteria { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsDefault { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedDate { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
