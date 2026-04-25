using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.UpdateTicketType;

/// <summary>
/// Handler for UpdateTicketTypeCommand.
/// Updates an existing ticket type in the database.
/// </summary>
public class UpdateTicketTypeCommandHandler : IRequestHandler<UpdateTicketTypeCommand, Int64>
{
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ILogger<UpdateTicketTypeCommandHandler> _logger;

    public UpdateTicketTypeCommandHandler(
        ITicketTypeRepository ticketTypeRepository,
        ILogger<UpdateTicketTypeCommandHandler> logger)
    {
        _ticketTypeRepository = ticketTypeRepository ?? throw new ArgumentNullException(nameof(ticketTypeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Int64> Handle(UpdateTicketTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating ticket type with ID: {TicketTypeId}", request.TicketTypeId);

            // Validate required fields
            if (request.TicketTypeId <= 0)
                throw new ArgumentException("Ticket type ID must be greater than zero");

            if (string.IsNullOrWhiteSpace(request.TypeNameAr))
                throw new ArgumentException("Arabic type name is required");

            if (string.IsNullOrWhiteSpace(request.TypeNameEn))
                throw new ArgumentException("English type name is required");

            if (request.DefaultPriorityId <= 0)
                throw new ArgumentException("Default priority ID must be greater than zero");

            if (request.SlaTargetHours <= 0)
                throw new ArgumentException("SLA target hours must be greater than zero");

            // Get existing ticket type
            var existingTicketType = await _ticketTypeRepository.GetByIdAsync(request.TicketTypeId);
            if (existingTicketType == null)
            {
                _logger.LogWarning("Ticket type not found with ID: {TicketTypeId}", request.TicketTypeId);
                return 0;
            }

            // Update entity
            var ticketType = new SysTicketType
            {
                RowId = request.TicketTypeId,
                TypeNameAr = request.TypeNameAr,
                TypeNameEn = request.TypeNameEn,
                DescriptionAr = request.DescriptionAr,
                DescriptionEn = request.DescriptionEn,
                DefaultPriorityId = request.DefaultPriorityId,
                SlaTargetHours = request.SlaTargetHours,
                IsActive = existingTicketType.IsActive,
                CreationUser = existingTicketType.CreationUser,
                CreationDate = existingTicketType.CreationDate,
                UpdateUser = request.UpdateUser,
                UpdateDate = DateTime.Now
            };

            var rowsAffected = await _ticketTypeRepository.UpdateAsync(ticketType);

            _logger.LogInformation("Ticket type updated successfully with ID: {TicketTypeId}", request.TicketTypeId);

            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket type with ID: {TicketTypeId}", request.TicketTypeId);
            throw;
        }
    }
}
