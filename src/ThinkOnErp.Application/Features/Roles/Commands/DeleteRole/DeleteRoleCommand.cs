using MediatR;

namespace ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;

/// <summary>
/// Command to delete (soft delete) a role from the system.
/// Sets IS_ACTIVE to false rather than physically removing the record.
/// Returns the number of rows affected (should be 1 for success).
/// </summary>
public class DeleteRoleCommand : IRequest<int>
{
    /// <summary>
    /// Unique identifier of the role to delete
    /// </summary>
    public decimal RowId { get; set; }
}
