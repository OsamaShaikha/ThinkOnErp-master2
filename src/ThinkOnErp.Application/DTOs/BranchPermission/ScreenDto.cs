namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class ScreenDto
{
    public long RowId { get; set; }
    public long? SystemId { get; set; }
    public string? SystemName { get; set; }
    public string? ScreenCode { get; set; }
    public string? ScreenName { get; set; }
    public string? ScreenNameE { get; set; }
    public string? Route { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
