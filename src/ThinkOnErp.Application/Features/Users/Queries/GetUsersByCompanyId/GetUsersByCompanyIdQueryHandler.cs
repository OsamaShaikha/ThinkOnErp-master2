using MediatR;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Features.Users.Queries.GetUsersByCompanyId;

public class GetUsersByCompanyIdQueryHandler : IRequestHandler<GetUsersByCompanyIdQuery, List<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersByCompanyIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<List<UserDto>> Handle(GetUsersByCompanyIdQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetByCompanyIdAsync(request.CompanyId);

        var userIds = users.Select(u => u.Id).ToList();
        var userBranches = await _userRepository.GetUserBranchesForUsersAsync(userIds);

        var branchesGrouped = userBranches.GroupBy(ub => ub.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return users.Select(u =>
        {
            var branches = branchesGrouped.TryGetValue(u.Id, out var list) ? list : new List<SysUserBranch>();
            var primaryBranch = branches.FirstOrDefault(b => b.IsPrimary);

            return new UserDto
            {
                UserId = u.Id,
                NameAr = u.FullNameAr,
                NameEn = u.FullNameEn,
                UserName = u.UserName,
                Phone = u.Phone,
                Phone2 = u.Phone2,
                RoleId = u.RoleId,
                BranchId = primaryBranch?.BranchId,
                BranchIds = branches.Select(b => b.BranchId).ToList(),
                PrimaryBranchId = primaryBranch?.BranchId,
                Email = u.Email,
                LastLoginDate = u.LastLoginDate,
                IsActive = u.IsActive,
                IsAdmin = u.IsAdmin,
                CreationUser = u.CreationUser,
                CreationDate = u.CreationDate,
                UpdateUser = u.UpdateUser,
                UpdateDate = u.UpdateDate
            };
        }).ToList();
    }
}
