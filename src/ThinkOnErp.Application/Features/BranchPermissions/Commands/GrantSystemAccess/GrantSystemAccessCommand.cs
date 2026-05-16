using MediatR;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemAccess;

public class GrantSystemAccessCommand : IRequest<long>
{
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public long GrantedBy { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
