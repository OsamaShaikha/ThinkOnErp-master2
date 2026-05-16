namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class GrantScreenAccessDto
{
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public string? Notes { get; set; }
    public string? GrantedBy { get; set; }
}
