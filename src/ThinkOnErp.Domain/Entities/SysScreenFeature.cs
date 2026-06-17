namespace ThinkOnErp.Domain.Entities;

public class SysScreenFeature
{
    public long ScreenId { get; set; }
    public long FeatureId { get; set; }
    public SysScreen? Screen { get; set; }
    public SysFeature? Feature { get; set; }
}
