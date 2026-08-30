using MediatR;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id);

        if (user == null)
            return null;

        var branches = await _userRepository.GetUserBranchesAsync(user.Id);
        var primaryBranch = branches.FirstOrDefault(b => b.IsPrimary);

        return new UserDto
        {
            UserId = user.Id,
            NameAr = user.FullNameAr,
            NameEn = user.FullNameEn,
            UserName = user.UserName,
            Phone = user.Phone,
            Phone2 = user.Phone2,
            RoleId = user.RoleId,
            BranchId = primaryBranch?.BranchId,
            BranchIds = branches.Select(b => b.BranchId).ToList(),
            PrimaryBranchId = primaryBranch?.BranchId,
            Email = user.Email,
            LastLoginDate = user.LastLoginDate,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            DefaultLang = user.DefaultLang,
            CreationUser = user.CreationUser,
            CreationDate = user.CreationDate,
            UpdateUser = user.UpdateUser,
            UpdateDate = user.UpdateDate
        };
    }
}
