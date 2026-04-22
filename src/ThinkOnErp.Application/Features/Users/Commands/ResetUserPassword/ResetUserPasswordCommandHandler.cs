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
    /// Generates a secure temporary password
    /// Format: Uppercase + Lowercase + Numbers + Special chars
    /// Length: 12 characters
    /// </summary>
    private string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string numbers = "0123456789";
        const string special = "!@#$%^&*";
        
        var random = new Random();
        var password = new char[12];
        
        // Ensure at least one of each type
        password[0] = uppercase[random.Next(uppercase.Length)];
        password[1] = lowercase[random.Next(lowercase.Length)];
        password[2] = numbers[random.Next(numbers.Length)];
        password[3] = special[random.Next(special.Length)];
        
        // Fill the rest randomly
        var allChars = uppercase + lowercase + numbers + special;
        for (int i = 4; i < 12; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }
        
        // Shuffle the password
        return new string(password.OrderBy(x => random.Next()).ToArray());
    }
}
