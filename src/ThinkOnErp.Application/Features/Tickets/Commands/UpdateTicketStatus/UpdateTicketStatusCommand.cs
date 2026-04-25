using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;

/// <summary>
/// Command for updating a ticket's status with workflow validation.
/// </summary>
public class UpdateTicketStatusCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket to update
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// New status ID for the ticket
    /// </summary>
    public Int64 NewStatusId { get; set; }

    /// <summary>
    /// Reason for the status change
    /// </summary>
    public string? StatusChangeReason { get; set; }

    /// <summary>
    /// Username of the user updating the status
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}