using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.ForceLogout;

/// <summary>
/// Handler for ForceLogoutCommand.
/// Forces logout of a user by invalidating all their tokens.
/// </summary>
public class ForceLogoutCommandHandler : IRequestHandler<ForceLogoutCommand, int>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ForceLogoutCommandHandler> _logger;

    public ForceLogoutCommandHandler(
        IUserRepository userRepository,
        ILogger<ForceLogoutCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(ForceLogoutCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Admin {AdminUser} forcing logout for user ID: {UserId}",
            request.AdminUser,
            request.UserId);

        var rowsAffected = await _userRepository.ForceLogoutAsync(
            request.UserId,
            request.AdminUser);

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Successfully forced logout for user ID: {UserId} by admin {AdminUser}",
                request.UserId,
                request.AdminUser);
        }
        else
        {
            _logger.LogWarning(
                "Force logout failed for user ID: {UserId}. User may not exist or is inactive.",
                request.UserId);
        }

        return rowsAffected;
    }
}
