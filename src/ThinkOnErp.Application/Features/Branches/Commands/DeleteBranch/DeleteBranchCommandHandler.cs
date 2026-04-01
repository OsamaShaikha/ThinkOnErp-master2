using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.DeleteBranch;

public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand, int>
{
    private readonly IBranchRepository _branchRepository;

    public DeleteBranchCommandHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<int> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        return await _branchRepository.DeleteAsync(request.RowId);
    }
}
