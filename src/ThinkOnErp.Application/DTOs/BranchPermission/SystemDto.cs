namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class SystemDto
{
    public long RowId { get; set; }
    public string? SystemCode { get; set; }
    public string? SystemName { get; set; }
    public string? SystemNameE { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
