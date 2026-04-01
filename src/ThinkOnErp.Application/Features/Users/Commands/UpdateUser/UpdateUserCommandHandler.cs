using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Int64>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Int64> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new SysUser
        {
            RowId = request.UserId,
            RowDesc = request.NameAr,
            RowDescE = request.NameEn,
            UserName = request.UserName,
            Phone = request.Phone,
            Phone2 = request.Phone2,
            Role = request.RoleId,
            BranchId = request.BranchId,
            Email = request.Email,
            IsAdmin = request.IsAdmin,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _userRepository.UpdateAsync(user);
    }
}
