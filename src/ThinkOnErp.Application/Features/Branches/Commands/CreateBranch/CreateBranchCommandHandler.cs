using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IPermissionRepository _permissionRepository;

    public CreateBranchCommandHandler(IBranchRepository branchRepository, IPermissionRepository permissionRepository)
    {
        _branchRepository = branchRepository;
        _permissionRepository = permissionRepository;
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
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        // Convert Base64 logo to byte array if provided
        if (!string.IsNullOrEmpty(request.BranchLogoBase64))
        {
            branch.BranchLogo = ConvertBase64ToBytes(request.BranchLogoBase64);
        }

        var branchId = await _branchRepository.CreateAsync(branch);

        // Grant systems and auto-grant all their screens if specified
        if (request.Systems?.Count > 0)
        {
            foreach (var systemId in request.Systems)
            {
                await _permissionRepository.SetBranchSystemAsync(
                    branchId, systemId, isAllowed: true,
                    grantedBy: null, notes: null,
                    creationUser: request.CreationUser
                );

                await _permissionRepository.GrantSystemScreensToBranchAsync(
                    branchId, systemId,
                    grantedBy: null,
                    creationUser: request.CreationUser
                );
            }
        }

        return branchId;
    }

    private static byte[] ConvertBase64ToBytes(string base64String)
    {
        // Remove data URL prefix if present (e.g., "data:image/jpeg;base64,")
        var base64Data = base64String;
        if (base64String.Contains(','))
        {
            base64Data = base64String.Split(',')[1];
        }

        return Convert.FromBase64String(base64Data);
    }
}
