using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTickets;

/// <summary>
/// Handler for GetTicketsQuery.
/// Retrieves tickets with filtering, sorting, and pagination.
/// </summary>
public class GetTicketsQueryHandler : IRequestHandler<GetTicketsQuery, PagedResult<TicketDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetTicketsQueryHandler> _logger;

    public GetTicketsQueryHandler(
        ITicketRepository ticketRepository,
        ILogger<GetTicketsQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<PagedResult<TicketDto>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving tickets with filters - Page: {Page}, PageSize: {PageSize}, SearchTerm: {SearchTerm}", 
            request.Page, request.PageSize, request.SearchTerm);

        try
        {
            // Validate pagination parameters
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Min(Math.Max(1, request.PageSize), 100); // Max 100 items per page

            // Get tickets from repository
            var (tickets, totalCount) = await _ticketRepository.GetAllAsync(
                companyId: request.CompanyId,
                branchId: request.BranchId,
                assigneeId: request.AssigneeId,
                statusId: request.StatusId,
                priorityId: request.PriorityId,
                typeId: request.TypeId,
                searchTerm: request.SearchTerm,
                createdFrom: request.CreatedFrom,
                createdTo: request.CreatedTo,
                page: page,
                pageSize: pageSize,
                sortBy: request.SortBy,
                sortDirection: request.SortDirection
            );

            // Map to DTOs
            var ticketDtos = tickets.Select(ticket => new TicketDto
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
                CommentCount = ticket.Comments?.Count ?? 0,
                AttachmentCount = ticket.Attachments?.Count ?? 0,
                SlaStatus = CalculateSlaStatus(ticket.ExpectedResolutionDate, ticket.ActualResolutionDate, ticket.TicketStatusId)
            }).ToList();

            var result = new PagedResult<TicketDto>
            {
                Items = ticketDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            _logger.LogInformation("Retrieved {Count} tickets out of {TotalCount} total tickets", 
                ticketDtos.Count, totalCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tickets");
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