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
        var branch = await _branchRepository.GetByIdAsync(request.BranchId);

        if (branch == null)
            return null;

        return new BranchDto
        {
            BranchId = branch.RowId,
            CompanyId = branch.ParRowId,
            BranchNameAr = branch.RowDesc,
            BranchNameEn = branch.RowDescE,
            Phone = branch.Phone,
            Mobile = branch.Mobile,
            Fax = branch.Fax,
            Email = branch.Email,
            IsHeadBranch = branch.IsHeadBranch,
            DefaultLang = branch.DefaultLang,
            BaseCurrencyId = branch.BaseCurrencyId,
            RoundingRules = branch.RoundingRules,
            HasLogo = branch.HasLogo,
            BranchLogoBase64 = ConvertBytesToBase64(branch.BranchLogo),
            IsActive = branch.IsActive,
            CreationUser = branch.CreationUser,
            CreationDate = branch.CreationDate,
            UpdateUser = branch.UpdateUser,
            UpdateDate = branch.UpdateDate
        };
    }

    private static string? ConvertBytesToBase64(byte[]? logoBytes)
    {
        if (logoBytes == null || logoBytes.Length == 0)
            return null;

        return $"data:image/jpeg;base64,{Convert.ToBase64String(logoBytes)}";
    }
}
