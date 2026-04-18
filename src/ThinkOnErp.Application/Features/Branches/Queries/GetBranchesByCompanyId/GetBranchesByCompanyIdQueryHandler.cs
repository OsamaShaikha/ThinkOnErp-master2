using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId;

public class GetBranchesByCompanyIdQueryHandler : IRequestHandler<GetBranchesByCompanyIdQuery, List<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchesByCompanyIdQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
    }

    public async Task<List<BranchDto>> Handle(GetBranchesByCompanyIdQuery request, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetByCompanyIdAsync(request.CompanyId);

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
            IsActive = b.IsActive,
            CreationUser = b.CreationUser,
            CreationDate = b.CreationDate,
            UpdateUser = b.UpdateUser,
            UpdateDate = b.UpdateDate,
            HasLogo = b.HasLogo,
            BranchLogoBase64 = ConvertBytesToBase64(b.BranchLogo)
        }).ToList();
    }

    private static string? ConvertBytesToBase64(byte[]? logoBytes)
    {
        if (logoBytes == null || logoBytes.Length == 0)
            return null;

        return $"data:image/jpeg;base64,{Convert.ToBase64String(logoBytes)}";
    }
}
