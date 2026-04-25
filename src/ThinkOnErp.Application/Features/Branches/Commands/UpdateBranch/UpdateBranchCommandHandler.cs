using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Int64>
{
    private readonly IBranchRepository _branchRepository;

    public UpdateBranchCommandHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<Int64> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new SysBranch
        {
            RowId = request.BranchId,
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
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        // Convert Base64 logo to byte array if provided
        if (!string.IsNullOrEmpty(request.BranchLogoBase64))
        {
            branch.BranchLogo = ConvertBase64ToBytes(request.BranchLogoBase64);
        }

        return await _branchRepository.UpdateAsync(branch);
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
