using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
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
    private readonly ILogger<GetTicketByIdQueryHandler> _logger;

    public GetTicketByIdQueryHandler(
        ITicketRepository ticketRepository,
        ITicketCommentRepository commentRepository,
        ITicketAttachmentRepository attachmentRepository,
        ILogger<GetTicketByIdQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _commentRepository = commentRepository;
        _attachmentRepository = attachmentRepository;
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

            // Map to DTO
            var ticketDto = new TicketDetailDto
            {
                TicketId = ticket.RowId,
                TitleAr = ticket.TitleAr,
                TitleEn = ticket.TitleEn,
                Description = ticket.Description,
                CompanyId = ticket.CompanyId,
                CompanyName = ticket.Company?.RowDescE,
                BranchId = ticket.BranchId,
                BranchName = ticket.Branch?.RowDescE,
                RequesterId = ticket.RequesterId,
                RequesterName = ticket.Requester?.RowDescE,
                AssigneeId = ticket.AssigneeId,
                AssigneeName = ticket.Assignee?.RowDescE,
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = ticket.TicketType?.TypeNameEn,
                TicketStatusId = ticket.TicketStatusId,
                TicketStatusName = ticket.TicketStatus?.StatusNameEn,
                TicketPriorityId = ticket.TicketPriorityId,
                TicketPriorityName = ticket.TicketPriority?.PriorityNameEn,
                TicketCategoryId = ticket.TicketCategoryId,
                TicketCategoryName = ticket.TicketCategory?.CategoryNameEn,
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
                        CommentId = c.RowId,
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