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
            BranchId = b.RowId,
            CompanyId = b.ParRowId,
            BranchNameAr = b.RowDesc,
            BranchNameEn = b.RowDescE,
            Phone = b.Phone,
            Mobile = b.Mobile,
            Fax = b.Fax,
            Email = b.Email,
            IsHeadBranch = b.IsHeadBranch,
            HasLogo = b.HasLogo,
            BranchLogoBase64 = ConvertBytesToBase64(b.BranchLogo),
            IsActive = b.IsActive,
            CreationUser = b.CreationUser,
            CreationDate = b.CreationDate,
            UpdateUser = b.UpdateUser,
            UpdateDate = b.UpdateDate
        }).ToList();
    }

    private static string? ConvertBytesToBase64(byte[]? logoBytes)
    {
        if (logoBytes == null || logoBytes.Length == 0)
            return null;

        return $"data:image/jpeg;base64,{Convert.ToBase64String(logoBytes)}";
    }
}
