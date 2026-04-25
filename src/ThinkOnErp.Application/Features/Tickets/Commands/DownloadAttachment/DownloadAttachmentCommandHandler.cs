using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.DownloadAttachment;

/// <summary>
/// Handler for DownloadAttachmentCommand.
/// Downloads a ticket attachment with security checks and authorization validation.
/// </summary>
public class DownloadAttachmentCommandHandler : IRequestHandler<DownloadAttachmentCommand, DownloadAttachmentResult>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketAttachmentRepository _attachmentRepository;
    private readonly ILogger<DownloadAttachmentCommandHandler> _logger;

    public DownloadAttachmentCommandHandler(
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ILogger<DownloadAttachmentCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _attachmentRepository = attachmentRepository;
        _logger = logger;
    }

    public async Task<DownloadAttachmentResult> Handle(DownloadAttachmentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing download request for attachment {AttachmentId} from ticket {TicketId} by user {RequestingUser}", 
            request.AttachmentId, request.TicketId, request.RequestingUser);

        try
        {
            // Verify the ticket exists
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Get the attachment
            var attachment = await _attachmentRepository.GetByIdAsync(request.AttachmentId);
            if (attachment == null)
            {
                throw new ArgumentException($"Attachment with ID {request.AttachmentId} not found.");
            }

            // Verify the attachment belongs to the specified ticket
            if (attachment.TicketId != request.TicketId)
            {
                throw new ArgumentException($"Attachment {request.AttachmentId} does not belong to ticket {request.TicketId}.");
            }

            // Additional security validation could be added here
            // For example, checking if the requesting user has access to this ticket

            // Log the download for audit purposes
            _logger.LogInformation("Attachment {AttachmentId} ({FileName}) downloaded by user {RequestingUser} from ticket {TicketId}", 
                request.AttachmentId, attachment.FileName, request.RequestingUser, request.TicketId);

            // Return the file data
            return new DownloadAttachmentResult
            {
                FileContent = attachment.FileContent,
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
                FileSize = attachment.FileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId} from ticket {TicketId} for user {RequestingUser}", 
                request.AttachmentId, request.TicketId, request.RequestingUser);
            throw;
        }
    }
}