using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetAllBranches;

public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;

    public GetAllBranchesQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<List<BranchDto>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetAllAsync();

        return branches.Select(b => new BranchDto
        {
            RowId = b.RowId,
            ParRowId = b.ParRowId,
            RowDesc = b.RowDesc,
            RowDescE = b.RowDescE,
            Phone = b.Phone,
            Mobile = b.Mobile,
            Fax = b.Fax,
            Email = b.Email,
            IsHeadBranch = b.IsHeadBranch,
            IsActive = b.IsActive,
            CreationUser = b.CreationUser,
            CreationDate = b.CreationDate,
            UpdateUser = b.UpdateUser,
            UpdateDate = b.UpdateDate
        }).ToList();
    }
}
