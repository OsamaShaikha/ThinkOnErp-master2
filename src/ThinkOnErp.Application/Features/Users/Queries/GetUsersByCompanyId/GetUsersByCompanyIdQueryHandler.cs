using MediatR;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Domain.Interfaces;

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

        return users.Select(u => new UserDto
        {
            UserId = u.RowId,
            NameAr = u.RowDesc,
            NameEn = u.RowDescE,
            UserName = u.UserName,
            Phone = u.Phone,
            Phone2 = u.Phone2,
            RoleId = u.Role,
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
