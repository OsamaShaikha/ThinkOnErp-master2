using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.SetBranchStatus;

public class SetBranchStatusCommand : IRequest<bool>
{
    public long BranchId { get; set; }
    public bool IsActive { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
