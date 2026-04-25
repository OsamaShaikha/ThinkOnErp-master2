using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;

/// <summary>
/// Command for adding a comment to a ticket.
/// </summary>
public class AddTicketCommentCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket to comment on
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Comment text content
    /// </summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is an internal comment (visible only to admins)
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Username of the user adding the comment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}