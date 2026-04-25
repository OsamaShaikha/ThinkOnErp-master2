using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for ticket notification service.
/// Handles sending notifications for ticket lifecycle events.
/// </summary>
public interface ITicketNotificationService
{
    /// <summary>
    /// Sends notification when a new ticket is created.
    /// </summary>
    /// <param name="ticket">The created ticket</param>
    /// <returns>Task representing the async operation</returns>
    Task SendTicketCreatedNotificationAsync(SysRequestTicket ticket);

    /// <summary>
    /// Sends notification when a ticket is assigned to a user.
    /// </summary>
    /// <param name="ticket">The assigned ticket</param>
    /// <param name="previousAssigneeId">Previous assignee ID (if any)</param>
    /// <returns>Task representing the async operation</returns>
    Task SendTicketAssignedNotificationAsync(SysRequestTicket ticket, Int64? previousAssigneeId = null);

    /// <summary>
    /// Sends notification when ticket status changes.
    /// </summary>
    /// <param name="ticket">The ticket with updated status</param>
    /// <param name="previousStatusId">Previous status ID</param>
    /// <returns>Task representing the async operation</returns>
    Task SendTicketStatusChangedNotificationAsync(SysRequestTicket ticket, Int64 previousStatusId);

    /// <summary>
    /// Sends notification when a comment is added to a ticket.
    /// </summary>
    /// <param name="ticket">The ticket</param>
    /// <param name="comment">The added comment</param>
    /// <returns>Task representing the async operation</returns>
    Task SendCommentAddedNotificationAsync(SysRequestTicket ticket, SysTicketComment comment);

    /// <summary>
    /// Sends SLA escalation alert for tickets approaching deadline.
    /// </summary>
    /// <param name="ticket">The ticket approaching SLA deadline</param>
    /// <returns>Task representing the async operation</returns>
    Task SendSlaEscalationAlertAsync(SysRequestTicket ticket);

    /// <summary>
    /// Sends notification when a file attachment is added to a ticket.
    /// </summary>
    /// <param name="ticket">The ticket</param>
    /// <param name="attachment">The added attachment</param>
    /// <returns>Task representing the async operation</returns>
    Task SendAttachmentAddedNotificationAsync(SysRequestTicket ticket, SysTicketAttachment attachment);
}