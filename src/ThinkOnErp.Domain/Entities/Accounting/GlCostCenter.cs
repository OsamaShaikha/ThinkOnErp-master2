namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlCostCenter
{
    public string CostCenterCode { get; set; } = string.Empty;
    public string? ParentCostCenterCode { get; set; }

    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public int CostCenterLevel { get; set; } = 1;
    public string CostCenterType { get; set; } = "DETAIL"; // HEADER, DETAIL

    public bool IsPostable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public GlCostCenter? ParentCostCenter { get; set; }
    public List<GlCostCenter> InverseParentCostCenter { get; set; } = new();
}
