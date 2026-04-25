using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;

/// <summary>
/// Command for assigning a ticket to a user.
/// Requires AdminOnly authorization.
/// </summary>
public class AssignTicketCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket to assign
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table (assignee)
    /// Set to null to unassign the ticket
    /// </summary>
    public Int64? AssigneeId { get; set; }

    /// <summary>
    /// Reason for the assignment change
    /// </summary>
    public string? AssignmentReason { get; set; }

    /// <summary>
    /// Username of the user making the assignment
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}