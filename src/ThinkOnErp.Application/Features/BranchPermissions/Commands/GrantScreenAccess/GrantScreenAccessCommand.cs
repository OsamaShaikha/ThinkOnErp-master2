using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantScreenAccess;

public class GrantScreenAccessCommand : IRequest<long>
{
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public string GrantedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
