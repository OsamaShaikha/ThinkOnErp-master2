namespace ThinkOnErp.Application.DTOs.Screen;

public class ScreenDto
{
    public long Id { get; set; }
    public long SystemId { get; set; }
    public long? ParentScreenId { get; set; }
    public string ScreenCode { get; set; } = string.Empty;
    public string ScreenName { get; set; } = string.Empty;
    public string ScreenNameE { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
