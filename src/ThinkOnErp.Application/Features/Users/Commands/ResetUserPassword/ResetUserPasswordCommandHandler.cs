using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.ResetUserPassword;

/// <summary>
/// Handler for ResetUserPasswordCommand
/// Generates a secure temporary password and updates the user's password
/// </summary>
public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, string>
{
    private readonly IUserRepository _userRepository;

    public ResetUserPasswordCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Handles the reset password command
    /// Generates a temporary password and returns it (password hashing happens in controller)
    /// </summary>
    public async Task<string> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId);
        
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found");
        }

        // Generate temporary password (will be hashed in controller before calling repository)
        var temporaryPassword = GenerateTemporaryPassword();

        return temporaryPassword;
    }

    /// <summary>
    /// Generates a cryptographically secure temporary password
    /// Format: Uppercase + Lowercase + Numbers + Special chars
    /// Length: 12 characters
    /// </summary>
    private string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string numbers = "0123456789";
        const string special = "!@#$%^&*";
        
        var allChars = uppercase + lowercase + numbers + special;
        var password = new char[12];
        var data = new byte[12];
        
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(data);
            password[0] = uppercase[data[0] % uppercase.Length];
            password[1] = lowercase[data[1] % lowercase.Length];
            password[2] = numbers[data[2] % numbers.Length];
            password[3] = special[data[3] % special.Length];
            
            for (int i = 4; i < 12; i++)
            {
                rng.GetBytes(data, i, 1);
                password[i] = allChars[data[i] % allChars.Length];
            }
        }
        
        // Fisher-Yates shuffle using crypto randomness
        var shuffleData = new byte[1];
        for (int i = password.Length - 1; i > 0; i--)
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(shuffleData);
            var j = shuffleData[0] % (i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }
        
        return new string(password);
    }
}
