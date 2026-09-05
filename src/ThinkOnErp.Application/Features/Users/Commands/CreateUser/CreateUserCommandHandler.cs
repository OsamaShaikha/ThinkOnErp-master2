using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Int64>
{
    private readonly IUserRepository _userRepository;
    private readonly IBranchRepository _branchRepository;

    public CreateUserCommandHandler(IUserRepository userRepository, IBranchRepository branchRepository)
    {
        _userRepository = userRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Int64> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.BranchIds == null || request.BranchIds.Count == 0)
        {
            throw new ArgumentException("User must be assigned to at least one branch.");
        }

        if (!request.BranchIds.Contains(request.PrimaryBranchId))
        {
            throw new ArgumentException("Primary branch must be one of the assigned branches.");
        }

        // Validate branch limits
        foreach (var branchId in request.BranchIds)
        {
            var branch = await _branchRepository.GetByIdAsync(branchId);
            if (branch == null)
            {
                throw new InvalidOperationException($"Branch with ID {branchId} not found.");
            }

            if (branch.UsersLimit.HasValue && branch.UsersLimit.Value > 0)
            {
                var currentCount = await _userRepository.GetBranchUserCountAsync(branchId);
                if (currentCount >= branch.UsersLimit.Value)
                {
                    throw new InvalidOperationException($"Users limit ({branch.UsersLimit.Value}) reached for branch '{branch.BranchNameEn}'.");
                }
            }
        }

        var primaryBranch = await _branchRepository.GetByIdAsync(request.PrimaryBranchId);

        var user = new SysUser
        {
            FullNameLocal = request.NameLocal,
            FullNameEn = request.NameEn,
            UserName = request.UserName,
            Password = request.Password, // Will be hashed in Infrastructure layer
            Phone = request.Phone,
            Phone2 = request.Phone2,
            RoleId = request.RoleId,
            CompanyId = primaryBranch?.CompanyId,
            Email = request.Email,
            IsAdmin = request.IsAdmin,
            DefaultLang = request.DefaultLang > 0 ? request.DefaultLang : 1,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        var userId = await _userRepository.CreateAsync(user);

        // Assign branches
        await _userRepository.AssignToBranchesAsync(userId, request.BranchIds, request.PrimaryBranchId, request.CreationUser);

        return userId;
    }
}
