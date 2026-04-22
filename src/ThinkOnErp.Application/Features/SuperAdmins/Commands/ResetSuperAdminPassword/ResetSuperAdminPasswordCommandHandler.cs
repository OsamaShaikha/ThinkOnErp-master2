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
