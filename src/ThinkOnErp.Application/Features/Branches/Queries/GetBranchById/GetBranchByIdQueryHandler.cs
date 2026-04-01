using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto?>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchByIdQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<BranchDto?> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.RowId);

        if (branch == null)
            return null;

        return new BranchDto
        {
            RowId = branch.RowId,
            ParRowId = branch.ParRowId,
            RowDesc = branch.RowDesc,
            RowDescE = branch.RowDescE,
            Phone = branch.Phone,
            Mobile = branch.Mobile,
            Fax = branch.Fax,
            Email = branch.Email,
            IsHeadBranch = branch.IsHeadBranch,
            IsActive = branch.IsActive,
            CreationUser = branch.CreationUser,
            CreationDate = branch.CreationDate,
            UpdateUser = branch.UpdateUser,
            UpdateDate = branch.UpdateDate
        };
    }
}
