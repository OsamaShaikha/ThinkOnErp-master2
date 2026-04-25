using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketAttachments;

/// <summary>
/// Handler for GetTicketAttachmentsQuery.
/// Retrieves attachments for a specific ticket with security validation.
/// </summary>
public class GetTicketAttachmentsQueryHandler : IRequestHandler<GetTicketAttachmentsQuery, List<TicketAttachmentDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketAttachmentRepository _attachmentRepository;
    private readonly ILogger<GetTicketAttachmentsQueryHandler> _logger;

    public GetTicketAttachmentsQueryHandler(
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ILogger<GetTicketAttachmentsQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _attachmentRepository = attachmentRepository;
        _logger = logger;
    }

    public async Task<List<TicketAttachmentDto>> Handle(GetTicketAttachmentsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving attachments for ticket {TicketId}", request.TicketId);

        try
        {
            // Verify the ticket exists
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Get attachments for the ticket
            var attachments = await _attachmentRepository.GetByTicketIdAsync(request.TicketId);

            // Map to DTOs
            var attachmentDtos = attachments.Select(a => new TicketAttachmentDto
            {
                AttachmentId = a.RowId,
                TicketId = a.TicketId,
                FileName = a.FileName,
                FileSize = a.FileSize,
                MimeType = a.MimeType,
                CreationUser = a.CreationUser,
                CreationUserName = a.CreationUser, // Using CreationUser from entity
                CreationDate = a.CreationDate,
                DownloadUrl = $"/api/tickets/{request.TicketId}/attachments/{a.RowId}"
            }).ToList();

            // Sort attachments
            if (request.SortDirection.ToUpperInvariant() == "DESC")
            {
                attachmentDtos = attachmentDtos.OrderByDescending(a => a.CreationDate).ToList();
            }
            else
            {
                attachmentDtos = attachmentDtos.OrderBy(a => a.CreationDate).ToList();
            }

            _logger.LogInformation("Retrieved {Count} attachments for ticket {TicketId}", 
                attachmentDtos.Count, request.TicketId);

            return attachmentDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for ticket {TicketId}", request.TicketId);
            throw;
        }
    }
}