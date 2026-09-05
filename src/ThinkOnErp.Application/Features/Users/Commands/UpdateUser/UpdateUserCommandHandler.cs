using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Int64>
{
    private readonly IUserRepository _userRepository;
    private readonly IBranchRepository _branchRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository, IBranchRepository branchRepository)
    {
        _userRepository = userRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Int64> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByIdAsync(request.UserId);
        if (existingUser == null)
        {
            return 0;
        }

        if (request.BranchIds == null || request.BranchIds.Count == 0)
        {
            throw new ArgumentException("User must be assigned to at least one branch.");
        }

        if (!request.BranchIds.Contains(request.PrimaryBranchId))
        {
            throw new ArgumentException("Primary branch must be one of the assigned branches.");
        }

        var existingBranchIds = await _userRepository.GetUserBranchIdsAsync(request.UserId);

        // Validate branch limits for new assignments
        foreach (var branchId in request.BranchIds)
        {
            var branch = await _branchRepository.GetByIdAsync(branchId);
            if (branch == null)
            {
                throw new InvalidOperationException($"Branch with ID {branchId} not found.");
            }

            if (!existingBranchIds.Contains(branchId))
            {
                if (branch.UsersLimit.HasValue && branch.UsersLimit.Value > 0)
                {
                    var currentCount = await _userRepository.GetBranchUserCountAsync(branchId);
                    if (currentCount >= branch.UsersLimit.Value)
                    {
                        throw new InvalidOperationException($"Users limit ({branch.UsersLimit.Value}) reached for branch '{branch.BranchNameEn}'.");
                    }
                }
            }
        }

        var primaryBranch = await _branchRepository.GetByIdAsync(request.PrimaryBranchId);

        var user = new SysUser
        {
            Id = request.UserId,
            FullNameLocal = request.NameLocal,
            FullNameEn = request.NameEn,
            UserName = request.UserName,
            Phone = request.Phone,
            Phone2 = request.Phone2,
            RoleId = request.RoleId,
            CompanyId = primaryBranch?.CompanyId,
            Email = request.Email,
            IsAdmin = request.IsAdmin,
            DefaultLang = request.DefaultLang > 0 ? request.DefaultLang : 1,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        var result = await _userRepository.UpdateAsync(user);

        // Update branch assignments
        if (result > 0)
        {
            await _userRepository.AssignToBranchesAsync(
                request.UserId,
                request.BranchIds,
                request.PrimaryBranchId,
                request.UpdateUser);
        }

        return result;
    }
}
