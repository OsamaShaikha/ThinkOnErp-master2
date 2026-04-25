using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicket;

/// <summary>
/// Handler for UpdateTicketCommand.
/// Updates an existing ticket with business rule validation.
/// </summary>
public class UpdateTicketCommandHandler : IRequestHandler<UpdateTicketCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ITicketPriorityRepository _ticketPriorityRepository;
    private readonly ILogger<UpdateTicketCommandHandler> _logger;

    public UpdateTicketCommandHandler(
        ITicketRepository ticketRepository,
        ITicketTypeRepository ticketTypeRepository,
        ITicketPriorityRepository ticketPriorityRepository,
        ILogger<UpdateTicketCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _ticketTypeRepository = ticketTypeRepository;
        _ticketPriorityRepository = ticketPriorityRepository;
        _logger = logger;
    }

    public async Task<Int64> Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating ticket {TicketId}", request.TicketId);

        try
        {
            // Get the existing ticket
            var existingTicket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (existingTicket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Validate that the ticket can be updated (not in final status)
            if (!await CanUpdateTicketAsync(existingTicket))
            {
                throw new InvalidOperationException("Cannot update ticket in final status (Closed or Cancelled).");
            }

            // Validate ticket type and priority exist
            var ticketType = await _ticketTypeRepository.GetByIdAsync(request.TicketTypeId);
            var ticketPriority = await _ticketPriorityRepository.GetByIdAsync(request.TicketPriorityId);

            if (ticketType == null)
            {
                throw new ArgumentException($"Ticket type with ID {request.TicketTypeId} not found.");
            }

            if (ticketPriority == null)
            {
                throw new ArgumentException($"Ticket priority with ID {request.TicketPriorityId} not found.");
            }

            // Update the ticket properties
            existingTicket.TitleAr = request.TitleAr;
            existingTicket.TitleEn = request.TitleEn;
            existingTicket.Description = request.Description;
            existingTicket.TicketTypeId = request.TicketTypeId;
            existingTicket.TicketPriorityId = request.TicketPriorityId;
            existingTicket.TicketCategoryId = request.TicketCategoryId;
            existingTicket.UpdateUser = request.UpdateUser;
            existingTicket.UpdateDate = DateTime.UtcNow;

            // Recalculate SLA if priority changed
            if (existingTicket.TicketPriorityId != request.TicketPriorityId)
            {
                existingTicket.ExpectedResolutionDate = CalculateExpectedResolutionDate(
                    existingTicket.CreationDate ?? DateTime.UtcNow, 
                    ticketPriority.SlaTargetHours);
            }

            // Update the ticket
            var result = await _ticketRepository.UpdateAsync(existingTicket);

            _logger.LogInformation("Ticket {TicketId} updated successfully", request.TicketId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket {TicketId}", request.TicketId);
            throw;
        }
    }

    /// <summary>
    /// Checks if the ticket can be updated based on its current status.
    /// </summary>
    private async Task<bool> CanUpdateTicketAsync(Domain.Entities.SysRequestTicket ticket)
    {
        // In a real implementation, you would check the status against final statuses
        // For now, we'll assume tickets can be updated unless they're in specific statuses
        // Status IDs: 1=Open, 2=In Progress, 3=Pending Customer, 4=Resolved, 5=Closed, 6=Cancelled
        var finalStatuses = new[] { 5L, 6L }; // Closed, Cancelled
        
        return !finalStatuses.Contains(ticket.TicketStatusId);
    }

    /// <summary>
    /// Calculates the expected resolution date based on creation date and SLA target hours.
    /// </summary>
    private DateTime CalculateExpectedResolutionDate(DateTime creationDate, decimal slaTargetHours)
    {
        var hoursToAdd = (double)slaTargetHours;
        return creationDate.AddHours(hoursToAdd);
    }
}