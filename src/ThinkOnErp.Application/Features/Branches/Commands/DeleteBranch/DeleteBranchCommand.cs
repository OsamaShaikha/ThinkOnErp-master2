using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.DeleteBranch;

public class DeleteBranchCommand : IRequest<int>
{
    public decimal RowId { get; set; }
}
