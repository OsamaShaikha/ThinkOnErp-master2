using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;

/// <summary>
/// Handler for AssignTicketCommand.
/// Assigns or unassigns a ticket to/from a user with AdminOnly authorization.
/// </summary>
public class AssignTicketCommandHandler : IRequestHandler<AssignTicketCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITicketNotificationService _notificationService;
    private readonly ILogger<AssignTicketCommandHandler> _logger;

    public AssignTicketCommandHandler(
        ITicketRepository ticketRepository,
        IUserRepository userRepository,
        ITicketNotificationService notificationService,
        ILogger<AssignTicketCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Int64> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Assigning ticket {TicketId} to user {AssigneeId}", 
            request.TicketId, request.AssigneeId);

        try
        {
            // Get the existing ticket
            var existingTicket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (existingTicket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Validate that the ticket can be assigned (not in final status)
            if (!await CanAssignTicketAsync(existingTicket))
            {
                throw new InvalidOperationException("Cannot assign ticket in final status (Closed or Cancelled).");
            }

            // If assigning to a user, validate the assignee exists and is an admin
            if (request.AssigneeId.HasValue)
            {
                var assignee = await _userRepository.GetByIdAsync(request.AssigneeId.Value);
                if (assignee == null)
                {
                    throw new ArgumentException($"User with ID {request.AssigneeId} not found.");
                }

                if (!assignee.IsAdmin)
                {
                    throw new ArgumentException("Only admin users can be assigned to tickets.");
                }

                if (!assignee.IsActive)
                {
                    throw new ArgumentException("Cannot assign tickets to inactive users.");
                }
            }

            // Update the ticket assignment
            var previousAssigneeId = existingTicket.AssigneeId;
            existingTicket.AssigneeId = request.AssigneeId;
            existingTicket.UpdateUser = request.UpdateUser;
            existingTicket.UpdateDate = DateTime.UtcNow;

            // If assigning for the first time and ticket is in Open status, move to In Progress
            if (request.AssigneeId.HasValue && previousAssigneeId == null && existingTicket.TicketStatusId == 1)
            {
                existingTicket.TicketStatusId = 2; // In Progress
            }

            // Update the ticket
            var result = await _ticketRepository.UpdateAsync(existingTicket);

            // Get updated ticket with navigation properties for notifications
            var updatedTicket = await _ticketRepository.GetByIdAsync(request.TicketId);

            // Send notification for assignment
            if (updatedTicket != null && request.AssigneeId.HasValue)
            {
                try
                {
                    await _notificationService.SendTicketAssignedNotificationAsync(updatedTicket, previousAssigneeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send assignment notification for ticket {TicketId}", request.TicketId);
                    // Don't fail the operation for notification failures
                }
            }

            var action = request.AssigneeId.HasValue ? "assigned to" : "unassigned from";
            var assigneeInfo = request.AssigneeId.HasValue ? $"user {request.AssigneeId}" : "all users";
            
            _logger.LogInformation("Ticket {TicketId} {Action} {AssigneeInfo} successfully", 
                request.TicketId, action, assigneeInfo);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning ticket {TicketId} to user {AssigneeId}", 
                request.TicketId, request.AssigneeId);
            throw;
        }
    }

    /// <summary>
    /// Checks if the ticket can be assigned based on its current status.
    /// </summary>
    private async Task<bool> CanAssignTicketAsync(Domain.Entities.SysRequestTicket ticket)
    {
        // Tickets in final statuses cannot be reassigned
        // Status IDs: 1=Open, 2=In Progress, 3=Pending Customer, 4=Resolved, 5=Closed, 6=Cancelled
        var finalStatuses = new[] { 5L, 6L }; // Closed, Cancelled
        
        return !finalStatuses.Contains(ticket.TicketStatusId);
    }
}