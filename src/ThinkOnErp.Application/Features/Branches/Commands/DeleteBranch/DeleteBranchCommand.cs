using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.DeleteBranch;

public class DeleteBranchCommand : IRequest<Int64>
{
    public Int64 BranchId { get; set; }
}
