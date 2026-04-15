using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Queries.CheckPermission;

/// <summary>
/// Handler for CheckPermissionQuery.
/// Checks if a user has permission to perform an action on a screen.
/// </summary>
public class CheckPermissionQueryHandler : IRequestHandler<CheckPermissionQuery, PermissionCheckResultDto>
{
    private readonly IPermissionRepository _permissionRepository;

    public CheckPermissionQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<PermissionCheckResultDto> Handle(CheckPermissionQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _permissionRepository.CheckUserPermissionAsync(
            request.UserId,
            request.ScreenCode,
            request.Action
        );

        return new PermissionCheckResultDto
        {
            Allowed = allowed,
            Reason = allowed ? null : "Permission denied"
        };
    }
}
