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
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _branchRepository.UpdateAsync(branch);
    }
}
