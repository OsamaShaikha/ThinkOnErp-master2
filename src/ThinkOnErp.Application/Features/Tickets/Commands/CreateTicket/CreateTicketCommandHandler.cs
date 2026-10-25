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
    private readonly ITicketStatusRepository _statuses;

    public CreateTicketCommandHandler(
        ITicketRepository ticketRepository,
        ITicketTypeRepository ticketTypeRepository,
        ITicketPriorityRepository ticketPriorityRepository,
        ITicketAttachmentRepository attachmentRepository,
        ITicketNotificationService notificationService,
        IAttachmentService attachmentService,
        ILogger<CreateTicketCommandHandler> logger,
        ITicketStatusRepository statuses)
    {
        _ticketRepository = ticketRepository;
        _ticketTypeRepository = ticketTypeRepository;
        _ticketPriorityRepository = ticketPriorityRepository;
        _attachmentRepository = attachmentRepository;
        _notificationService = notificationService;
        _attachmentService = attachmentService;
        _logger = logger;
        _statuses = statuses;
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

            if (ticketType == null || !ticketType.IsActive)
            {
                throw new ArgumentException($"Ticket type with ID {request.TicketTypeId} not found.");
            }

            if (ticketPriority == null || !ticketPriority.IsActive)
            {
                throw new ArgumentException($"Ticket priority with ID {request.TicketPriorityId} not found.");
            }

            // Calculate expected resolution date based on SLA
            var createdUtc = DateTime.UtcNow;
            var expectedResolutionDate = createdUtc.AddHours((double)ticketPriority.SlaTargetHours);

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
                CreationDate = createdUtc
            };

            // Validate every attachment before persisting anything. EF saves the
            // ticket and its attachment graph in one atomic SaveChanges call.
            ticket.Attachments = await PrepareAttachmentsAsync(request.Attachments, request.CreationUser);
            // Create the ticket
            var ticketId = await _ticketRepository.CreateAsync(ticket);

            _logger.LogInformation("Ticket created successfully with ID {TicketId}", ticketId);

            // Get the created ticket with navigation properties for notifications
            var createdTicket = await _ticketRepository.GetByIdAsync(ticketId);

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
    /// Gets the "Open" status ID from the database.
    /// </summary>
    private async Task<Int64> GetOpenStatusIdAsync()
    {
        var status = await _statuses.GetByCodeAsync(SysTicketStatus.StatusCodes.Open);
        if (status == null || TicketWorkflow.IsFinal(status))
            throw new InvalidOperationException("An active non-final OPEN status must be configured.");
        return status.Id;
    }

    /// <summary>
    /// Processes and saves file attachments for the ticket.
    /// </summary>
    private async Task<List<SysTicketAttachment>> PrepareAttachmentsAsync(List<ThinkOnErp.Application.DTOs.Ticket.CreateAttachmentDto>? attachments, string creationUser)
    {
        var prepared = new List<SysTicketAttachment>();
        if (attachments == null) return prepared;
        if (attachments.Count > 5) throw new ArgumentException("Maximum 5 attachments allowed per ticket.");

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
                    FileName = attachmentDto.FileName,
                    FileSize = fileBytes.Length,
                    MimeType = attachmentDto.MimeType,
                    FileContent = fileBytes,
                    CreationUser = creationUser,
                    CreationDate = DateTime.UtcNow
                };

                prepared.Add(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating attachment {FileName}", attachmentDto.FileName);
                throw;
            }
        }
        return prepared;
    }
}
