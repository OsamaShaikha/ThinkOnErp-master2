using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketTypes.Queries.GetTicketTypeById;

/// <summary>
/// Handler for GetTicketTypeByIdQuery.
/// Retrieves a specific ticket type by its ID from the database.
/// </summary>
public class GetTicketTypeByIdQueryHandler : IRequestHandler<GetTicketTypeByIdQuery, TicketTypeDto?>
{
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ILogger<GetTicketTypeByIdQueryHandler> _logger;

    public GetTicketTypeByIdQueryHandler(
        ITicketTypeRepository ticketTypeRepository,
        ILogger<GetTicketTypeByIdQueryHandler> logger)
    {
        _ticketTypeRepository = ticketTypeRepository ?? throw new ArgumentNullException(nameof(ticketTypeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TicketTypeDto?> Handle(GetTicketTypeByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrieving ticket type with ID: {TicketTypeId}", request.TicketTypeId);

            var ticketType = await _ticketTypeRepository.GetByIdAsync(request.TicketTypeId);

            if (ticketType == null)
            {
                _logger.LogWarning("Ticket type not found with ID: {TicketTypeId}", request.TicketTypeId);
                return null;
            }

            var dto = new TicketTypeDto
            {
                TicketTypeId = ticketType.RowId,
                TypeNameAr = ticketType.TypeNameAr,
                TypeNameEn = ticketType.TypeNameEn,
                DescriptionAr = ticketType.DescriptionAr,
                DescriptionEn = ticketType.DescriptionEn,
                DefaultPriorityId = ticketType.DefaultPriorityId,
                DefaultPriorityName = ticketType.DefaultPriority?.PriorityNameEn,
                SlaTargetHours = ticketType.SlaTargetHours,
                IsActive = ticketType.IsActive,
                CreationUser = ticketType.CreationUser,
                CreationDate = ticketType.CreationDate,
                UpdateUser = ticketType.UpdateUser,
                UpdateDate = ticketType.UpdateDate
            };

            _logger.LogInformation("Retrieved ticket type with ID: {TicketTypeId}", request.TicketTypeId);

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ticket type with ID: {TicketTypeId}", request.TicketTypeId);
            throw;
        }
    }
}
