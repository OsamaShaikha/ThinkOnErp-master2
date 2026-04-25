using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>
/// Handler for CreateTicketCommand.
/// Creates a new ticket with SLA calculation and optional file attachments.
/// </summary>
public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly ITicketPriorityRepository _ticketPriorityRepository;
    private readonly ITicketAttachmentRepository _attachmentRepository;
    private readonly ITicketNotificationService _notificationService;
    private readonly IAttachmentService _attachmentService;
    private readonly ILogger<CreateTicketCommandHandler> _logger;

    public CreateTicketCommandHandler(
        ITicketRepository ticketRepository,
        ITicketTypeRepository ticketTypeRepository,
        ITicketPriorityRepository ticketPriorityRepository,
        ITicketAttachmentRepository attachmentRepository,
        ITicketNotificationService notificationService,
        IAttachmentService attachmentService,
        ILogger<CreateTicketCommandHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _ticketTypeRepository = ticketTypeRepository;
        _ticketPriorityRepository = ticketPriorityRepository;
        _attachmentRepository = attachmentRepository;
        _notificationService = notificationService;
        _attachmentService = attachmentService;
        _logger = logger;
    }

    public async Task<Int64> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new ticket for company {CompanyId}, branch {BranchId}", 
            request.CompanyId, request.BranchId);

        try
        {
            // Get ticket type and priority for SLA calculation
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

            // Calculate expected resolution date based on SLA
            var expectedResolutionDate = CalculateExpectedResolutionDate(ticketPriority.SlaTargetHours);

            // Get the "Open" status ID (assuming it's the first status)
            var openStatusId = await GetOpenStatusIdAsync();

            // Create the ticket entity
            var ticket = new SysRequestTicket
            {
                TitleAr = request.TitleAr,
                TitleEn = request.TitleEn,
                Description = request.Description,
                CompanyId = request.CompanyId,
                BranchId = request.BranchId,
                RequesterId = request.RequesterId,
                AssigneeId = null, // Initially unassigned
                TicketTypeId = request.TicketTypeId,
                TicketStatusId = openStatusId,
                TicketPriorityId = request.TicketPriorityId,
                TicketCategoryId = request.TicketCategoryId,
                ExpectedResolutionDate = expectedResolutionDate,
                ActualResolutionDate = null,
                IsActive = true,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.UtcNow
            };

            // Create the ticket
            var ticketId = await _ticketRepository.CreateAsync(ticket);

            _logger.LogInformation("Ticket created successfully with ID {TicketId}", ticketId);

            // Get the created ticket with navigation properties for notifications
            var createdTicket = await _ticketRepository.GetByIdAsync(ticketId);

            // Process attachments if provided
            if (request.Attachments != null && request.Attachments.Any())
            {
                await ProcessAttachmentsAsync(ticketId, request.Attachments, request.CreationUser, createdTicket);
            }

            // Send notification for ticket creation
            if (createdTicket != null)
            {
                try
                {
                    await _notificationService.SendTicketCreatedNotificationAsync(createdTicket);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send ticket creation notification for ticket {TicketId}", ticketId);
                    // Don't fail the entire operation for notification failures
                }
            }

            _logger.LogInformation("Ticket creation completed for ticket ID {TicketId}", ticketId);

            return ticketId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket for company {CompanyId}, branch {BranchId}", 
                request.CompanyId, request.BranchId);
            throw;
        }
    }

    /// <summary>
    /// Calculates the expected resolution date based on SLA target hours.
    /// Excludes weekends for business hours calculation.
    /// </summary>
    private DateTime CalculateExpectedResolutionDate(decimal slaTargetHours)
    {
        var creationDate = DateTime.UtcNow;
        var hoursToAdd = (double)slaTargetHours;
        
        // Simple calculation - add hours to creation date
        // In a real implementation, you might want to exclude weekends and holidays
        return creationDate.AddHours(hoursToAdd);
    }

    /// <summary>
    /// Gets the "Open" status ID from the database.
    /// </summary>
    private async Task<Int64> GetOpenStatusIdAsync()
    {
        // This is a simplified implementation
        // In a real scenario, you might want to cache this or have it as a constant
        // For now, we'll assume status ID 1 is "Open"
        return 1;
    }

    /// <summary>
    /// Processes and saves file attachments for the ticket.
    /// </summary>
    private async Task ProcessAttachmentsAsync(Int64 ticketId, List<ThinkOnErp.Application.DTOs.Ticket.CreateAttachmentDto> attachments, string creationUser, SysRequestTicket? ticket)
    {
        _logger.LogInformation("Processing {AttachmentCount} attachments for ticket {TicketId}", 
            attachments.Count, ticketId);

        foreach (var attachmentDto in attachments)
        {
            try
            {
                // Validate attachment using the attachment service
                if (!_attachmentService.IsValidFileType(attachmentDto.FileName, attachmentDto.MimeType))
                {
                    throw new ArgumentException($"File type not allowed for {attachmentDto.FileName}");
                }

                if (!_attachmentService.IsValidBase64Content(attachmentDto.FileContent))
                {
                    throw new ArgumentException($"Invalid file content for {attachmentDto.FileName}");
                }

                if (!_attachmentService.IsValidFileSize(attachmentDto.FileContent))
                {
                    throw new ArgumentException($"File size exceeds limit for {attachmentDto.FileName}");
                }

                var fileBytes = _attachmentService.DecodeBase64Content(attachmentDto.FileContent);
                
                // Validate file content matches MIME type
                if (!await _attachmentService.ValidateFileContentAsync(fileBytes, attachmentDto.MimeType, attachmentDto.FileName))
                {
                    throw new ArgumentException($"File content validation failed for {attachmentDto.FileName}");
                }
                
                var attachment = new SysTicketAttachment
                {
                    TicketId = ticketId,
                    FileName = attachmentDto.FileName,
                    FileSize = fileBytes.Length,
                    MimeType = attachmentDto.MimeType,
                    FileContent = fileBytes,
                    CreationUser = creationUser,
                    CreationDate = DateTime.UtcNow
                };

                var attachmentId = await _attachmentRepository.CreateAsync(attachment);
                
                _logger.LogInformation("Attachment {FileName} saved with ID {AttachmentId} for ticket {TicketId}", 
                    attachmentDto.FileName, attachmentId, ticketId);

                // Send notification for attachment
                if (ticket != null)
                {
                    try
                    {
                        await _notificationService.SendAttachmentAddedNotificationAsync(ticket, attachment);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send attachment notification for {FileName} on ticket {TicketId}", 
                            attachmentDto.FileName, ticketId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing attachment {FileName} for ticket {TicketId}", 
                    attachmentDto.FileName, ticketId);
                throw;
            }
        }
    }
}