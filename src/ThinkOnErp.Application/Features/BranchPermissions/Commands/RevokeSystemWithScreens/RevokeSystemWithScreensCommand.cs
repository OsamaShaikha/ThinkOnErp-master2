using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemWithScreens;

public class RevokeSystemWithScreensCommand : IRequest
{
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
