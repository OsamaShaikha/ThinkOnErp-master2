namespace ThinkOnErp.Application.DTOs.BranchPermission;

public class GrantSystemAccessDto
{
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public string? Notes { get; set; }
    public string? GrantedBy { get; set; }
}
