using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.CreateTicketType;

/// <summary>
/// Handler for CreateTicketTypeCommand.
/// Creates a new ticket type in the database.
/// </summary>
public class CreateTicketTypeCommandHandler : IRequestHandler<CreateTicketTypeCommand, Int64>
{
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ILogger<CreateTicketTypeCommandHandler> _logger;

    public CreateTicketTypeCommandHandler(
        ITicketTypeRepository ticketTypeRepository,
        ILogger<CreateTicketTypeCommandHandler> logger)
    {
        _ticketTypeRepository = ticketTypeRepository ?? throw new ArgumentNullException(nameof(ticketTypeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Int64> Handle(CreateTicketTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating new ticket type: {TypeNameEn}", request.TypeNameEn);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.TypeNameAr))
                throw new ArgumentException("Arabic type name is required");

            if (string.IsNullOrWhiteSpace(request.TypeNameEn))
                throw new ArgumentException("English type name is required");

            if (request.DefaultPriorityId <= 0)
                throw new ArgumentException("Default priority ID must be greater than zero");

            if (request.SlaTargetHours <= 0)
                throw new ArgumentException("SLA target hours must be greater than zero");

            // Create entity
            var ticketType = new SysTicketType
            {
                TypeNameAr = request.TypeNameAr,
                TypeNameEn = request.TypeNameEn,
                DescriptionAr = request.DescriptionAr,
                DescriptionEn = request.DescriptionEn,
                DefaultPriorityId = request.DefaultPriorityId,
                SlaTargetHours = request.SlaTargetHours,
                IsActive = true,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.Now
            };

            var ticketTypeId = await _ticketTypeRepository.CreateAsync(ticketType);

            _logger.LogInformation("Ticket type created successfully with ID: {TicketTypeId}", ticketTypeId);

            return ticketTypeId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket type: {TypeNameEn}", request.TypeNameEn);
            throw;
        }
    }
}
