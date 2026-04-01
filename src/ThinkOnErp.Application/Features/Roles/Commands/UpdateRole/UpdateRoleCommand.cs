using MediatR;

namespace ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;

/// <summary>
/// Command to update an existing role in the system.
/// Returns the number of rows affected (should be 1 for success).
/// </summary>
public class UpdateRoleCommand : IRequest<int>
{
    /// <summary>
    /// Unique identifier of the role to update
    /// </summary>
    public decimal RowId { get; set; }

    /// <summary>
    /// Arabic description of the role (required)
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the role (required)
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Username of the user updating this role
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}
