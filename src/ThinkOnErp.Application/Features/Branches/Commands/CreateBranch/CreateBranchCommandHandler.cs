using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogoStorageService _logoStorageService;

    public CreateBranchCommandHandler(IBranchRepository branchRepository,
        ILogoStorageService logoStorageService)
    {
        _branchRepository = branchRepository;
        _logoStorageService = logoStorageService;
    }

    public async Task<Int64> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new SysBranch
        {
            CompanyId = request.CompanyId,
            BranchNameAr = request.BranchNameAr,
            BranchNameEn = request.BranchNameEn,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Fax = request.Fax,
            Email = request.Email,
            TaxNumber = request.TaxNumber,
            IsHeadBranch = request.IsHeadBranch,
            DefaultLang = request.DefaultLang,
            BaseCurrencyId = request.BaseCurrencyId,
            RoundingRules = request.RoundingRules,
            UsersLimit = request.UsersLimit,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        if (request.BranchLogo != null)
        {
            var branchIdTmp = 0L;
            branch.BranchLogoPath = await _logoStorageService.SaveLogoAsync(request.BranchLogo, "branches", branchIdTmp);
        }

        var branchId = await _branchRepository.CreateAsync(branch);

        // Update the saved file with correct ID
        if (request.BranchLogo != null && branch.BranchLogoPath != null)
        {
            var oldPath = branch.BranchLogoPath;
            branch.BranchLogoPath = await _logoStorageService.SaveLogoAsync(request.BranchLogo, "branches", branchId);
            branch.Id = branchId;
            branch.UpdateUser = request.CreationUser;
            branch.UpdateDate = DateTime.UtcNow;
            await _branchRepository.UpdateAsync(branch);
            await _logoStorageService.DeleteLogoAsync(oldPath);
        }

        return branchId;
    }
}
