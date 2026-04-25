using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.DeleteTicketType;

/// <summary>
/// Handler for DeleteTicketTypeCommand.
/// Performs soft delete of a ticket type by setting IsActive to false.
/// </summary>
public class DeleteTicketTypeCommandHandler : IRequestHandler<DeleteTicketTypeCommand, Int64>
{
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<DeleteTicketTypeCommandHandler> _logger;

    public DeleteTicketTypeCommandHandler(
        ITicketTypeRepository ticketTypeRepository,
        ITicketRepository ticketRepository,
        ILogger<DeleteTicketTypeCommandHandler> logger)
    {
        _ticketTypeRepository = ticketTypeRepository ?? throw new ArgumentNullException(nameof(ticketTypeRepository));
        _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Int64> Handle(DeleteTicketTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deleting ticket type with ID: {TicketTypeId}", request.TicketTypeId);

            if (request.TicketTypeId <= 0)
                throw new ArgumentException("Ticket type ID must be greater than zero");

            // Check if ticket type exists
            var ticketType = await _ticketTypeRepository.GetByIdAsync(request.TicketTypeId);
            if (ticketType == null)
            {
                _logger.LogWarning("Ticket type not found with ID: {TicketTypeId}", request.TicketTypeId);
                return 0;
            }

            // Check if there are active tickets using this type
            // Note: This would require a method in ITicketRepository to check for active tickets by type
            // For now, we'll proceed with the delete and let the database handle referential integrity

            var rowsAffected = await _ticketTypeRepository.DeleteAsync(request.TicketTypeId, request.DeleteUser);

            _logger.LogInformation("Ticket type deleted successfully with ID: {TicketTypeId}", request.TicketTypeId);

            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ticket type with ID: {TicketTypeId}", request.TicketTypeId);
            throw;
        }
    }
}
