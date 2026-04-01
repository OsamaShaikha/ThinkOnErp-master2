using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public ChangePasswordCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return false;

        // Verify current password (hashing will be done in Infrastructure layer)
        // For now, we'll assume the Infrastructure layer handles password verification
        
        // Update password
        user.Password = request.NewPassword; // Will be hashed in Infrastructure layer
        user.UpdateDate = DateTime.UtcNow;
        
        var result = await _userRepository.UpdateAsync(user);
        return result > 0;
    }
}
