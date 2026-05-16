using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemAccess;

public class RevokeSystemAccessCommand : IRequest<long>
{
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
