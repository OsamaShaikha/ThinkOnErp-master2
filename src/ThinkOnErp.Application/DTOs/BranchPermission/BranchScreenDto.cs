namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class BranchScreenDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public string? ScreenCode { get; set; }
    public string? ScreenName { get; set; }
    public string? ScreenNameE { get; set; }
    public long? SystemId { get; set; }
    public string? SystemName { get; set; }
    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public long? GrantedBy { get; set; }
    public string? GrantedByName { get; set; }
    public DateTime? GrantedDate { get; set; }
    public string? Notes { get; set; }
}
