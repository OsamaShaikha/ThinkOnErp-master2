using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;

    public CreateBranchCommandHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<Int64> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new SysBranch
        {
            ParRowId = request.CompanyId,
            RowDesc = request.BranchNameAr,
            RowDescE = request.BranchNameEn,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Fax = request.Fax,
            Email = request.Email,
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

        return await _branchRepository.CreateAsync(branch);
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
