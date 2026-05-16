using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantScreenAccess;

public class GrantScreenAccessCommand : IRequest<long>
{
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public long GrantedBy { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
