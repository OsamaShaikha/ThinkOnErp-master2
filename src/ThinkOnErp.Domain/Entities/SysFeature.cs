namespace ThinkOnErp.Domain.Entities;

public class SysFeature
{
    public Int64 Id { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string FeatureNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public ICollection<SysScreenFeature>? ScreenFeatures { get; set; }
}
