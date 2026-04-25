using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;

/// <summary>
/// Handler for AddTicketCommentCommand.
/// Adds a comment to a ticket with authorization validation.
/// </summary>
public class AddTicketCommentCommandHandler : IRequestHandler<AddTicketCommentCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketCommentRepository _commentRepository;
    private readonly ITicketNotificationService _notificationService;
    private readonly ILogger<AddTicketCommentCommandHandler> _logger;

    public AddTicketCommentCommandHandler(
        ITicketRepository ticketRepository,
        ITicketCommentRepository commentRepository,
        ITicketNotificationService notificationService,
        ILogger<AddTicketCommentCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _commentRepository = commentRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Int64> Handle(AddTicketCommentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding comment to ticket {TicketId}", request.TicketId);

        try
        {
            // Verify the ticket exists
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Validate that comments can be added to this ticket
            if (!await CanAddCommentToTicketAsync(ticket))
            {
                throw new InvalidOperationException("Cannot add comments to tickets in final status (Closed or Cancelled).");
            }

            // Create the comment entity
            var comment = new SysTicketComment
            {
                TicketId = request.TicketId,
                CommentText = request.CommentText,
                IsInternal = request.IsInternal,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.UtcNow
            };

            // Save the comment
            var commentId = await _commentRepository.CreateAsync(comment);

            _logger.LogInformation("Comment {CommentId} added to ticket {TicketId} successfully", 
                commentId, request.TicketId);

            // Send notification for comment (only for non-internal comments)
            if (!request.IsInternal)
            {
                try
                {
                    await _notificationService.SendCommentAddedNotificationAsync(ticket, comment);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send comment notification for ticket {TicketId}", request.TicketId);
                    // Don't fail the operation for notification failures
                }
            }

            return commentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to ticket {TicketId}", request.TicketId);
            throw;
        }
    }

    /// <summary>
    /// Checks if comments can be added to the ticket based on its current status.
    /// </summary>
    private async Task<bool> CanAddCommentToTicketAsync(SysRequestTicket ticket)
    {
        // Comments cannot be added to tickets in final statuses
        // Status IDs: 1=Open, 2=In Progress, 3=Pending Customer, 4=Resolved, 5=Closed, 6=Cancelled
        var finalStatuses = new[] { 5L, 6L }; // Closed, Cancelled
        
        return !finalStatuses.Contains(ticket.TicketStatusId);
    }
}