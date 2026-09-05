using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.DTOs.Accounting.CostCenters;

public sealed class GlCostCenterTreeDto
{
    public string CostCenterCode { get; set; } = string.Empty;
    public string? ParentCostCenterCode { get; set; }

    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public int CostCenterLevel { get; set; }
    public string CostCenterType { get; set; } = string.Empty;

    public bool IsPostable { get; set; }
    public bool IsActive { get; set; }

    [JsonPropertyOrder(100)]
    public List<GlCostCenterTreeDto> Children { get; set; } = new();
}
