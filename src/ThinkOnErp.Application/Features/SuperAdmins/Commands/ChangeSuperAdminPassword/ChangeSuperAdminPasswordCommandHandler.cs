using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangeSuperAdminPassword;

/// <summary>
/// Handler for changing super admin password
/// </summary>
public class ChangeSuperAdminPasswordCommandHandler : IRequestHandler<ChangeSuperAdminPasswordCommand, bool>
{
    private readonly ISuperAdminRepository _superAdminRepository;

    public ChangeSuperAdminPasswordCommandHandler(ISuperAdminRepository superAdminRepository)
    {
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
    }

    public async Task<bool> Handle(ChangeSuperAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        // Get super admin to verify existence and current password
        var superAdmin = await _superAdminRepository.GetByIdAsync(request.SuperAdminId);
        
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        // Note: Current password verification and new password hashing will be done in the API controller
        // This follows clean architecture - the Application layer doesn't know about hashing
        // The request.NewPassword should already be hashed when it reaches here
        
        // Change password using repository method
        var rowsAffected = await _superAdminRepository.ChangePasswordAsync(
            request.SuperAdminId,
            request.NewPassword, // Already hashed in controller
            request.UpdateUser);

        return rowsAffected > 0;
    }
}
