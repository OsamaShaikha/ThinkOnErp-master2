using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemWithScreens;

public class GrantSystemWithScreensCommand : IRequest
{
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public long GrantedBy { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
