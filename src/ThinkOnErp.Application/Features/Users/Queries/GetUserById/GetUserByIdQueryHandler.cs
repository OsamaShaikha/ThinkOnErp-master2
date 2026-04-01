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
        var user = await _userRepository.GetByIdAsync(request.RowId);

        if (user == null)
            return null;

        return new UserDto
        {
            UserId = user.RowId,
            NameAr = user.RowDesc,
            NameEn = user.RowDescE,
            UserName = user.UserName,
            Phone = user.Phone,
            Phone2 = user.Phone2,
            RoleId = user.Role,
            BranchId = user.BranchId,
            Email = user.Email,
            LastLoginDate = user.LastLoginDate,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            CreationUser = user.CreationUser,
            CreationDate = user.CreationDate,
            UpdateUser = user.UpdateUser,
            UpdateDate = user.UpdateDate
        };
    }
}
