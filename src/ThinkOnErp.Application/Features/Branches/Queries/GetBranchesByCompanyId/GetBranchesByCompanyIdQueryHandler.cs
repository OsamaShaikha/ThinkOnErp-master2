using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId;

public class GetBranchesByCompanyIdQueryHandler : IRequestHandler<GetBranchesByCompanyIdQuery, List<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetBranchesByCompanyIdQueryHandler(IBranchRepository branchRepository, ILogoStorageService logoStorageService)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _logoStorageService = logoStorageService ?? throw new ArgumentNullException(nameof(logoStorageService));
    }

    public async Task<List<BranchDto>> Handle(GetBranchesByCompanyIdQuery request, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetByCompanyIdAsync(request.CompanyId);

        var result = new List<BranchDto>();
        foreach (var b in branches)
        {
            string? logoBase64 = null;
            if (b.BranchLogoPath != null)
            {
                var logoBytes = await _logoStorageService.GetLogoAsync(b.BranchLogoPath);
                logoBase64 = ConvertBytesToBase64(logoBytes);
            }

            result.Add(new BranchDto
            {
                BranchId = b.Id,
                CompanyId = b.CompanyId,
                BranchNameAr = b.BranchNameAr,
                BranchNameEn = b.BranchNameEn,
                Phone = b.Phone,
                Mobile = b.Mobile,
                Fax = b.Fax,
                Email = b.Email,
                TaxNumber = b.TaxNumber,
                IsHeadBranch = b.IsHeadBranch,
                DefaultLang = b.DefaultLang,
                BaseCurrencyId = b.BaseCurrencyId,
                RoundingRules = b.RoundingRules,
                IsActive = b.IsActive,
                CreationUser = b.CreationUser,
                CreationDate = b.CreationDate,
                UpdateUser = b.UpdateUser,
                UpdateDate = b.UpdateDate,
                HasLogo = b.BranchLogoPath != null,
                BranchLogoBase64 = logoBase64
            });
        }

        return result;
    }

    private static string? ConvertBytesToBase64(byte[]? logoBytes)
    {
        if (logoBytes == null || logoBytes.Length == 0)
            return null;

        return $"data:image/jpeg;base64,{Convert.ToBase64String(logoBytes)}";
    }
}
