using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;

/// <summary>
/// Handler for UploadAttachmentCommand.
/// Uploads a file attachment to a ticket with validation and security checks.
/// </summary>
public class UploadAttachmentCommandHandler : IRequestHandler<UploadAttachmentCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketAttachmentRepository _attachmentRepository;
    private readonly IAttachmentService _attachmentService;
    private readonly ITicketNotificationService _notificationService;
    private readonly ILogger<UploadAttachmentCommandHandler> _logger;

    public UploadAttachmentCommandHandler(
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        IAttachmentService attachmentService,
        ITicketNotificationService notificationService,
        ILogger<UploadAttachmentCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentService = attachmentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Int64> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading attachment {FileName} to ticket {TicketId}", 
            request.FileName, request.TicketId);

        try
        {
            // Verify the ticket exists
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Validate that attachments can be added to this ticket
            if (!await CanAddAttachmentToTicketAsync(ticket))
            {
                throw new InvalidOperationException("Cannot add attachments to tickets in final status (Closed or Cancelled).");
            }

            // Check current attachment count
            var currentAttachmentCount = await _attachmentRepository.GetAttachmentCountAsync(request.TicketId);
            var maxAttachmentCount = _attachmentService.GetMaxAttachmentCount();
            if (currentAttachmentCount >= maxAttachmentCount)
            {
                throw new InvalidOperationException($"Maximum of {maxAttachmentCount} attachments allowed per ticket.");
            }

            // Validate file using attachment service
            if (!_attachmentService.IsValidFileType(request.FileName, request.MimeType))
            {
                throw new ArgumentException("File type is not allowed.");
            }

            if (!_attachmentService.IsValidBase64Content(request.FileContent))
            {
                throw new ArgumentException("Invalid Base64 file content.");
            }

            if (!_attachmentService.IsValidFileSize(request.FileContent))
            {
                throw new ArgumentException($"File size exceeds the maximum limit of {_attachmentService.GetMaxFileSizeBytes() / (1024 * 1024)} MB.");
            }

            // Decode and validate file content
            byte[] fileBytes = _attachmentService.DecodeBase64Content(request.FileContent);

            // Validate file content matches MIME type
            if (!await _attachmentService.ValidateFileContentAsync(fileBytes, request.MimeType, request.FileName))
            {
                throw new ArgumentException("File content does not match declared MIME type.");
            }

            // Create the attachment entity
            var attachment = new SysTicketAttachment
            {
                TicketId = request.TicketId,
                FileName = request.FileName,
                FileSize = fileBytes.Length,
                MimeType = request.MimeType,
                FileContent = fileBytes,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.UtcNow
            };

            // Save the attachment
            var attachmentId = await _attachmentRepository.CreateAsync(attachment);

            _logger.LogInformation("Attachment {FileName} uploaded successfully with ID {AttachmentId} for ticket {TicketId}", 
                request.FileName, attachmentId, request.TicketId);

            // Send notification for attachment upload
            try
            {
                await _notificationService.SendAttachmentAddedNotificationAsync(ticket, attachment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send attachment notification for {FileName} on ticket {TicketId}", 
                    request.FileName, request.TicketId);
                // Don't fail the operation for notification failures
            }

            return attachmentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment {FileName} to ticket {TicketId}", 
                request.FileName, request.TicketId);
            throw;
        }
    }

    /// <summary>
    /// Checks if attachments can be added to the ticket based on its current status.
    /// </summary>
    private async Task<bool> CanAddAttachmentToTicketAsync(SysRequestTicket ticket)
    {
        // Attachments cannot be added to tickets in final statuses
        // Status IDs: 1=Open, 2=In Progress, 3=Pending Customer, 4=Resolved, 5=Closed, 6=Cancelled
        var finalStatuses = new[] { 5L, 6L }; // Closed, Cancelled
        
        return !finalStatuses.Contains(ticket.TicketStatusId);
    }
}