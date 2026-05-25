using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto?>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetBranchByIdQueryHandler(IBranchRepository branchRepository, ILogoStorageService logoStorageService)
    {
        _branchRepository = branchRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<BranchDto?> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId);

        if (branch == null)
            return null;

        string? logoBase64 = null;
        if (branch.BranchLogoPath != null)
        {
            var logoBytes = await _logoStorageService.GetLogoAsync(branch.BranchLogoPath);
            logoBase64 = ConvertBytesToBase64(logoBytes);
        }

        return new BranchDto
        {
            BranchId = branch.Id,
            CompanyId = branch.CompanyId,
            BranchNameAr = branch.BranchNameAr,
            BranchNameEn = branch.BranchNameEn,
            Phone = branch.Phone,
            Mobile = branch.Mobile,
            Fax = branch.Fax,
            Email = branch.Email,
            TaxNumber = branch.TaxNumber,
            IsHeadBranch = branch.IsHeadBranch,
            DefaultLang = branch.DefaultLang,
            BaseCurrencyId = branch.BaseCurrencyId,
            RoundingRules = branch.RoundingRules,
            HasLogo = branch.BranchLogoPath != null,
            BranchLogoBase64 = logoBase64,
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
