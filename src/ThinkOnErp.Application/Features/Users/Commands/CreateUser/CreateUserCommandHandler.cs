using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Int64>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Int64> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new SysUser
        {
            RowDesc = request.NameAr,
            RowDescE = request.NameEn,
            UserName = request.UserName,
            Password = request.Password, // Will be hashed in Infrastructure layer
            Phone = request.Phone,
            Phone2 = request.Phone2,
            Role = request.RoleId,
            BranchId = request.BranchId,
            Email = request.Email,
            IsAdmin = request.IsAdmin,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _userRepository.CreateAsync(user);
    }
}
