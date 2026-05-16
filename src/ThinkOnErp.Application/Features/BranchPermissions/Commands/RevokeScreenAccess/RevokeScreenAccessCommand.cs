using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeScreenAccess;

public class RevokeScreenAccessCommand : IRequest<long>
{
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
