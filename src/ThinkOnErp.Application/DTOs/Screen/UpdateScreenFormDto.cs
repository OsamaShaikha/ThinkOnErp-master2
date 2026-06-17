namespace ThinkOnErp.Application.DTOs.Screen;

public class UpdateScreenFormDto
{
    public long SystemId { get; set; }
    public long? ParentScreenId { get; set; }
    public string ScreenCode { get; set; } = string.Empty;
    public string ScreenName { get; set; } = string.Empty;
    public string ScreenNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public int DisplayOrder { get; set; }
}
