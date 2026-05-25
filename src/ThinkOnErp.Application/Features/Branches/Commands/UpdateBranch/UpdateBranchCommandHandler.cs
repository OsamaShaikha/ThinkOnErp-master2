using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;

    public UpdateBranchCommandHandler(IBranchRepository branchRepository, ILogoStorageService logoStorageService)
    {
        _branchRepository = branchRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<Int64> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var existing = await _branchRepository.GetByIdAsync(request.BranchId);
        if (existing == null) return 0;

        existing.CompanyId = request.CompanyId;
        existing.BranchNameAr = request.BranchNameAr;
        existing.BranchNameEn = request.BranchNameEn;
        existing.Phone = request.Phone;
        existing.Mobile = request.Mobile;
        existing.Fax = request.Fax;
        existing.Email = request.Email;
        existing.TaxNumber = request.TaxNumber;
        existing.IsHeadBranch = request.IsHeadBranch;
        existing.DefaultLang = request.DefaultLang;
        existing.BaseCurrencyId = request.BaseCurrencyId;
        existing.RoundingRules = request.RoundingRules;
        existing.UpdateUser = request.UpdateUser;
        existing.UpdateDate = DateTime.UtcNow;

        if (request.BranchLogo != null)
        {
            if (existing.BranchLogoPath != null)
                await _logoStorageService.DeleteLogoAsync(existing.BranchLogoPath);

            existing.BranchLogoPath = await _logoStorageService.SaveLogoAsync(request.BranchLogo, "branches", request.BranchId);
        }

        return await _branchRepository.UpdateAsync(existing);
    }
}
