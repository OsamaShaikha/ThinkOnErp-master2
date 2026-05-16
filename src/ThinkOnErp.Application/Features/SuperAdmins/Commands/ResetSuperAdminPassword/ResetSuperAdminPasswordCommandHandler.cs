using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ResetSuperAdminPassword;

/// <summary>
/// Handler for resetting super admin password
/// Generates a secure temporary password
/// </summary>
public class ResetSuperAdminPasswordCommandHandler : IRequestHandler<ResetSuperAdminPasswordCommand, string>
{
    private readonly ISuperAdminRepository _superAdminRepository;

    public ResetSuperAdminPasswordCommandHandler(ISuperAdminRepository superAdminRepository)
    {
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
    }

    public async Task<string> Handle(ResetSuperAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        // Verify super admin exists
        var superAdmin = await _superAdminRepository.GetByIdAsync(request.SuperAdminId);
        
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        // Generate temporary password (will be hashed in controller)
        var temporaryPassword = GenerateTemporaryPassword();

        // Note: Password will be hashed in the controller before being passed here
        // The request should contain the hashed password when it reaches this handler
        
        // This handler expects the password to already be hashed
        // The actual password reset happens in the controller
        
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
