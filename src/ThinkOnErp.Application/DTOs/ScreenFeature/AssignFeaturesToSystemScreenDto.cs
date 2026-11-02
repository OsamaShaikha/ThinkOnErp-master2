namespace ThinkOnErp.Application.DTOs.ScreenFeature;

public class AssignFeaturesToSystemScreenDto
{
    public long SystemId { get; set; }
    public long? ScreenId { get; set; }
    public List<long> FeatureIds { get; set; } = new();
}
