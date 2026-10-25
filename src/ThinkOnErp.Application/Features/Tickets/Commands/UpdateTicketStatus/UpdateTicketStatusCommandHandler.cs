using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;

/// <summary>
/// Handler for UpdateTicketStatusCommand.
/// Updates ticket status with workflow validation and automatic resolution date setting.
/// </summary>
public class UpdateTicketStatusCommandHandler : IRequestHandler<UpdateTicketStatusCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketStatusRepository _ticketStatusRepository;
    private readonly ITicketNotificationService _notificationService;
    private readonly ILogger<UpdateTicketStatusCommandHandler> _logger;

    public UpdateTicketStatusCommandHandler(
        ITicketRepository ticketRepository,
        ITicketStatusRepository ticketStatusRepository,
        ITicketNotificationService notificationService,
        ILogger<UpdateTicketStatusCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _ticketStatusRepository = ticketStatusRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Int64> Handle(UpdateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status of ticket {TicketId} to status {NewStatusId}", 
            request.TicketId, request.NewStatusId);

        try
        {
            // Get the existing ticket
            var existingTicket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (existingTicket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Get the new status
            var newStatus = await _ticketStatusRepository.GetByIdAsync(request.NewStatusId);
            if (newStatus == null || !newStatus.IsActive)
            {
                throw new ArgumentException($"Status with ID {request.NewStatusId} not found.");
            }

            // Validate status transition
            var previousStatus = await _ticketStatusRepository.GetByIdAsync(existingTicket.TicketStatusId);
            if (!TicketWorkflow.CanTransition(previousStatus, newStatus))
            {
                throw new InvalidOperationException($"Invalid status transition from {existingTicket.TicketStatusId} to {request.NewStatusId}.");
            }

            // Store previous status for logging
            var previousStatusId = existingTicket.TicketStatusId;

            // Update the ticket status
            existingTicket.TicketStatusId = request.NewStatusId;
            existingTicket.UpdateUser = request.UpdateUser;
            existingTicket.UpdateDate = DateTime.UtcNow;

            // Set resolution date if moving to Resolved status
            if (TicketWorkflow.Code(newStatus) == "RESOLVED" && existingTicket.ActualResolutionDate == null)
            {
                existingTicket.ActualResolutionDate = DateTime.UtcNow;
            }

            // Clear resolution date if moving away from Resolved status
            if (TicketWorkflow.Code(previousStatus) == "RESOLVED" && TicketWorkflow.Code(newStatus) != "RESOLVED" && TicketWorkflow.Code(newStatus) != "CLOSED")
            {
                existingTicket.ActualResolutionDate = null;
            }

            // Update the ticket
            var result = await _ticketRepository.UpdateAsync(existingTicket);

            // Get updated ticket with navigation properties for notifications
            var updatedTicket = await _ticketRepository.GetByIdAsync(request.TicketId);

            // Send notification for status change
            if (updatedTicket != null)
            {
                try
                {
                    await _notificationService.SendTicketStatusChangedNotificationAsync(updatedTicket, previousStatusId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send status change notification for ticket {TicketId}", request.TicketId);
                    // Don't fail the operation for notification failures
                }
            }

            _logger.LogInformation("Ticket {TicketId} status updated from {PreviousStatusId} to {NewStatusId} successfully", 
                request.TicketId, previousStatusId, request.NewStatusId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status of ticket {TicketId} to status {NewStatusId}", 
                request.TicketId, request.NewStatusId);
            throw;
        }
    }

}
