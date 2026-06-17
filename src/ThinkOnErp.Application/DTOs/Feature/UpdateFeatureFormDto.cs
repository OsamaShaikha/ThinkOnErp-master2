namespace ThinkOnErp.Application.DTOs.Feature;

public class UpdateFeatureFormDto
{
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string FeatureNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public int DisplayOrder { get; set; }
}
