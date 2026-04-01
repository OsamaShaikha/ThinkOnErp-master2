using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, int>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<int> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new SysUser
        {
            RowId = request.RowId,
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            UserName = request.UserName,
            Phone = request.Phone,
            Phone2 = request.Phone2,
            Role = request.Role,
            BranchId = request.BranchId,
            Email = request.Email,
            IsAdmin = request.IsAdmin,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _userRepository.UpdateAsync(user);
    }
}
