namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class BranchScreenDto
{
    public long RowId { get; set; }
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public string? ScreenCode { get; set; }
    public string? ScreenName { get; set; }
    public string? ScreenNameE { get; set; }
    public long? SystemId { get; set; }
    public string? SystemName { get; set; }
    public bool IsAllowed { get; set; }
    public long? GrantedBy { get; set; }
    public string? GrantedByName { get; set; }
    public DateTime? GrantedDate { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? Notes { get; set; }
}
