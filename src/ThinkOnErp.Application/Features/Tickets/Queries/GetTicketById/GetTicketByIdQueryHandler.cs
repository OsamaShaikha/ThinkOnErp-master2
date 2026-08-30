using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Services.Localization;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketById;

/// <summary>
/// Handler for GetTicketByIdQuery.
/// Retrieves a specific ticket with detailed information including comments and attachments.
/// </summary>
public class GetTicketByIdQueryHandler : IRequestHandler<GetTicketByIdQuery, TicketDetailDto?>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketCommentRepository _commentRepository;
    private readonly ITicketAttachmentRepository _attachmentRepository;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<GetTicketByIdQueryHandler> _logger;

    public GetTicketByIdQueryHandler(
        ITicketRepository ticketRepository,
        ITicketCommentRepository commentRepository,
        ITicketAttachmentRepository attachmentRepository,
        ILocalizationService localizationService,
        ILogger<GetTicketByIdQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _commentRepository = commentRepository;
        _attachmentRepository = attachmentRepository;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<TicketDetailDto?> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving ticket {TicketId}", request.TicketId);

        try
        {
            // Get the ticket
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                _logger.LogWarning("Ticket {TicketId} not found", request.TicketId);
                return null;
            }

            var isArabic = _localizationService.ResolveLanguageId(null) == 1;

            // Map to DTO with language-aware names and safe fallbacks
            var ticketDto = new TicketDetailDto
            {
                TicketId = ticket.Id,
                TitleAr = ticket.TitleAr,
                TitleEn = ticket.TitleEn,
                Description = ticket.Description,
                CompanyId = ticket.CompanyId,
                CompanyName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.Company?.CompanyNameAr) ? ticket.Company.CompanyNameAr : ticket.Company?.CompanyNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.Company?.CompanyNameEn) ? ticket.Company.CompanyNameEn : ticket.Company?.CompanyNameAr),
                BranchId = ticket.BranchId,
                BranchName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.Branch?.BranchNameAr) ? ticket.Branch.BranchNameAr : ticket.Branch?.BranchNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.Branch?.BranchNameEn) ? ticket.Branch.BranchNameEn : ticket.Branch?.BranchNameAr),
                RequesterId = ticket.RequesterId,
                RequesterName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.Requester?.FullNameAr) ? ticket.Requester.FullNameAr : ticket.Requester?.FullNameEn ?? ticket.CreationUser)
                    : (!string.IsNullOrWhiteSpace(ticket.Requester?.FullNameEn) ? ticket.Requester.FullNameEn : ticket.Requester?.FullNameAr ?? ticket.CreationUser),
                AssigneeId = ticket.AssigneeId,
                AssigneeName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.Assignee?.FullNameAr) ? ticket.Assignee.FullNameAr : ticket.Assignee?.FullNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.Assignee?.FullNameEn) ? ticket.Assignee.FullNameEn : ticket.Assignee?.FullNameAr),
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.TicketType?.TypeNameAr) ? ticket.TicketType.TypeNameAr : ticket.TicketType?.TypeNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.TicketType?.TypeNameEn) ? ticket.TicketType.TypeNameEn : ticket.TicketType?.TypeNameAr),
                TicketStatusId = ticket.TicketStatusId,
                TicketStatusName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.TicketStatus?.StatusNameAr) ? ticket.TicketStatus.StatusNameAr : ticket.TicketStatus?.StatusNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.TicketStatus?.StatusNameEn) ? ticket.TicketStatus.StatusNameEn : ticket.TicketStatus?.StatusNameAr),
                TicketPriorityId = ticket.TicketPriorityId,
                TicketPriorityName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.TicketPriority?.PriorityNameAr) ? ticket.TicketPriority.PriorityNameAr : ticket.TicketPriority?.PriorityNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.TicketPriority?.PriorityNameEn) ? ticket.TicketPriority.PriorityNameEn : ticket.TicketPriority?.PriorityNameAr),
                TicketCategoryId = ticket.TicketCategoryId,
                TicketCategoryName = isArabic 
                    ? (!string.IsNullOrWhiteSpace(ticket.TicketCategory?.CategoryNameAr) ? ticket.TicketCategory.CategoryNameAr : ticket.TicketCategory?.CategoryNameEn)
                    : (!string.IsNullOrWhiteSpace(ticket.TicketCategory?.CategoryNameEn) ? ticket.TicketCategory.CategoryNameEn : ticket.TicketCategory?.CategoryNameAr),
                ExpectedResolutionDate = ticket.ExpectedResolutionDate,
                ActualResolutionDate = ticket.ActualResolutionDate,
                IsActive = ticket.IsActive,
                CreationUser = ticket.CreationUser,
                CreationDate = ticket.CreationDate,
                UpdateUser = ticket.UpdateUser,
                UpdateDate = ticket.UpdateDate,
                SlaStatus = CalculateSlaStatus(ticket.ExpectedResolutionDate, ticket.ActualResolutionDate, ticket.TicketStatusId)
            };

            // Load comments if requested
            if (request.IncludeComments)
            {
                var comments = await _commentRepository.GetByTicketIdAsync(request.TicketId, request.IncludeInternalComments);
                
                ticketDto.Comments = comments.Select(c => new TicketCommentDto
                    {
                        CommentId = c.Id,
                        TicketId = c.TicketId,
                        CommentText = c.CommentText,
                        IsInternal = c.IsInternal,
                        CreationUser = c.CreationUser,
                        CreationUserName = c.CreationUser, // Using CreationUser from entity
                        CreationDate = c.CreationDate
                    })
                    .OrderBy(c => c.CreationDate)
                    .ToList();

                ticketDto.CommentCount = ticketDto.Comments.Count;
            }

            // Load attachments if requested
            if (request.IncludeAttachments)
            {
                var attachments = await _attachmentRepository.GetByTicketIdAsync(request.TicketId);
                
                ticketDto.Attachments = attachments.Select(a => new TicketAttachmentDto
                {
                    AttachmentId = a.Id,
                    TicketId = a.TicketId,
                    FileName = a.FileName,
                    FileSize = a.FileSize,
                    MimeType = a.MimeType,
                    CreationUser = a.CreationUser,
                    CreationUserName = a.CreationUser, // Using CreationUser from entity
                    CreationDate = a.CreationDate,
                    DownloadUrl = $"/api/tickets/{request.TicketId}/attachments/{a.Id}"
                }).ToList();

                ticketDto.AttachmentCount = ticketDto.Attachments.Count;
            }

            _logger.LogInformation("Successfully retrieved ticket {TicketId} with {CommentCount} comments and {AttachmentCount} attachments", 
                request.TicketId, ticketDto.CommentCount, ticketDto.AttachmentCount);

            return ticketDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ticket {TicketId}", request.TicketId);
            throw;
        }
    }

    /// <summary>
    /// Calculates the SLA status based on expected resolution date and current status.
    /// </summary>
    private string CalculateSlaStatus(DateTime? expectedResolutionDate, DateTime? actualResolutionDate, Int64 statusId)
    {
        if (expectedResolutionDate == null)
            return "Unknown";

        // If ticket is resolved or closed, check if it was on time
        if (statusId == 4 || statusId == 5) // Resolved or Closed
        {
            if (actualResolutionDate == null)
                return "OnTime"; // Assume on time if no actual resolution date

            return actualResolutionDate <= expectedResolutionDate ? "OnTime" : "Overdue";
        }

        // For open tickets, check against current time
        var now = DateTime.UtcNow;
        var timeToDeadline = expectedResolutionDate.Value - now;

        if (timeToDeadline.TotalHours < 0)
            return "Overdue";
        
        if (timeToDeadline.TotalHours <= 2) // At risk if less than 2 hours remaining
            return "AtRisk";
        
        return "OnTime";
    }
}