using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, decimal>
{
    private readonly IBranchRepository _branchRepository;

    public CreateBranchCommandHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<decimal> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new SysBranch
        {
            ParRowId = request.ParRowId,
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Fax = request.Fax,
            Email = request.Email,
            IsHeadBranch = request.IsHeadBranch,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _branchRepository.CreateAsync(branch);
    }
}
