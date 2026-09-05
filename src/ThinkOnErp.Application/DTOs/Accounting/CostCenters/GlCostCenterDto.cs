namespace ThinkOnErp.Application.DTOs.Accounting.CostCenters;

public sealed class GlCostCenterDto
{
    public string CostCenterCode { get; set; } = string.Empty;
    public string? ParentCostCenterCode { get; set; }

    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public int CostCenterLevel { get; set; }
    public string CostCenterType { get; set; } = string.Empty;

    public bool IsPostable { get; set; }
    public bool IsActive { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
