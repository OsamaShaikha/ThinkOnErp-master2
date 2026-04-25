using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketTypes.Queries.GetAllTicketTypes;

/// <summary>
/// Handler for GetAllTicketTypesQuery.
/// Retrieves all active ticket types from the database.
/// </summary>
public class GetAllTicketTypesQueryHandler : IRequestHandler<GetAllTicketTypesQuery, List<TicketTypeDto>>
{
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ILogger<GetAllTicketTypesQueryHandler> _logger;

    public GetAllTicketTypesQueryHandler(
        ITicketTypeRepository ticketTypeRepository,
        ILogger<GetAllTicketTypesQueryHandler> logger)
    {
        _ticketTypeRepository = ticketTypeRepository ?? throw new ArgumentNullException(nameof(ticketTypeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<TicketTypeDto>> Handle(GetAllTicketTypesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrieving all ticket types");

            var ticketTypes = await _ticketTypeRepository.GetAllAsync();

            var dtos = ticketTypes.Select(tt => new TicketTypeDto
            {
                TicketTypeId = tt.RowId,
                TypeNameAr = tt.TypeNameAr,
                TypeNameEn = tt.TypeNameEn,
                DescriptionAr = tt.DescriptionAr,
                DescriptionEn = tt.DescriptionEn,
                DefaultPriorityId = tt.DefaultPriorityId,
                DefaultPriorityName = tt.DefaultPriority?.PriorityNameEn,
                SlaTargetHours = tt.SlaTargetHours,
                IsActive = tt.IsActive,
                CreationUser = tt.CreationUser,
                CreationDate = tt.CreationDate,
                UpdateUser = tt.UpdateUser,
                UpdateDate = tt.UpdateDate
            }).ToList();

            _logger.LogInformation("Retrieved {Count} ticket types", dtos.Count);

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all ticket types");
            throw;
        }
    }
}
