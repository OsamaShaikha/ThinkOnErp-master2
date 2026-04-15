using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;

namespace ThinkOnErp.Application.Features.Permissions.Queries.CheckPermission;

/// <summary>
/// Query to check if a user has permission to perform an action on a screen.
/// </summary>
public class CheckPermissionQuery : IRequest<PermissionCheckResultDto>
{
    /// <summary>
    /// User ID to check permissions for
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Screen code to check access to
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Action to check: VIEW, INSERT, UPDATE, DELETE
    /// </summary>
    public string Action { get; set; } = string.Empty;
}
