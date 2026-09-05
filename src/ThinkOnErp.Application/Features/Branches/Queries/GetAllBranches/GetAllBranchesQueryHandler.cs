using MediatR;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetAllBranches;

public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;

    public GetAllBranchesQueryHandler(IBranchRepository branchRepository, ILogoStorageService logoStorageService)
    {
        _branchRepository = branchRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<List<BranchDto>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetAllAsync();

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
                BranchNameLocal = b.BranchNameLocal,
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
                HasLogo = b.BranchLogoPath != null,
                BranchLogoBase64 = logoBase64,
                IsActive = b.IsActive,
                CreationUser = b.CreationUser,
                CreationDate = b.CreationDate,
                UpdateUser = b.UpdateUser,
                UpdateDate = b.UpdateDate
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
