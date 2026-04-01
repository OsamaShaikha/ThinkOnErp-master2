using MediatR;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, List<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<List<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync();

        return users.Select(u => new UserDto
        {
            RowId = u.RowId,
            RowDesc = u.RowDesc,
            RowDescE = u.RowDescE,
            UserName = u.UserName,
            Phone = u.Phone,
            Phone2 = u.Phone2,
            Role = u.Role,
            BranchId = u.BranchId,
            Email = u.Email,
            LastLoginDate = u.LastLoginDate,
            IsActive = u.IsActive,
            IsAdmin = u.IsAdmin,
            CreationUser = u.CreationUser,
            CreationDate = u.CreationDate,
            UpdateUser = u.UpdateUser,
            UpdateDate = u.UpdateDate
        }).ToList();
    }
}
