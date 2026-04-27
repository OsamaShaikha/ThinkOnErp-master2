using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTickets;

/// <summary>
/// Handler for GetTicketsQuery.
/// Retrieves tickets with filtering, sorting, and pagination.
/// Logs search analytics for performance optimization.
/// Requirements: 8.1-8.12, 8.11, 19.9
/// </summary>
public class GetTicketsQueryHandler : IRequestHandler<GetTicketsQuery, PagedResult<TicketDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ISearchAnalyticsRepository _searchAnalyticsRepository;
    private readonly ILogger<GetTicketsQueryHandler> _logger;

    public GetTicketsQueryHandler(
        ITicketRepository ticketRepository,
        ISearchAnalyticsRepository searchAnalyticsRepository,
        ILogger<GetTicketsQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _searchAnalyticsRepository = searchAnalyticsRepository;
        _logger = logger;
    }

    public async Task<PagedResult<TicketDto>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Retrieving tickets with filters - Page: {Page}, PageSize: {PageSize}, SearchTerm: {SearchTerm}, UseAdvancedSearch: {UseAdvancedSearch}", 
            request.Page, request.PageSize, request.SearchTerm, request.UseAdvancedSearch);

        try
        {
            // Validate pagination parameters
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Min(Math.Max(1, request.PageSize), 100); // Max 100 items per page

            List<SysRequestTicket> tickets;
            int totalCount;

            // Use advanced search if requested or if multi-criteria filtering is needed
            if (request.UseAdvancedSearch || !string.IsNullOrEmpty(request.StatusIds) || 
                !string.IsNullOrEmpty(request.PriorityIds) || !string.IsNullOrEmpty(request.TypeIds) ||
                !string.IsNullOrEmpty(request.CategoryIds) || request.FilterLogic == "OR")
            {
                // Use advanced search with relevance scoring
                (tickets, totalCount) = await _ticketRepository.AdvancedSearchAsync(
                    searchTerm: request.SearchTerm,
                    companyId: request.CompanyId,
                    branchId: request.BranchId,
                    assigneeId: request.AssigneeId,
                    requesterId: request.RequesterId,
                    statusIds: request.StatusIds ?? (request.StatusId.HasValue ? request.StatusId.Value.ToString() : null),
                    priorityIds: request.PriorityIds ?? (request.PriorityId.HasValue ? request.PriorityId.Value.ToString() : null),
                    typeIds: request.TypeIds ?? (request.TypeId.HasValue ? request.TypeId.Value.ToString() : null),
                    categoryIds: request.CategoryIds ?? (request.CategoryId.HasValue ? request.CategoryId.Value.ToString() : null),
                    createdFrom: request.CreatedFrom,
                    createdTo: request.CreatedTo,
                    dueFrom: request.DueFrom,
                    dueTo: request.DueTo,
                    slaStatus: request.SlaStatus,
                    filterLogic: request.FilterLogic,
                    includeInactive: request.IncludeInactive,
                    page: page,
                    pageSize: pageSize,
                    sortBy: request.SortBy,
                    sortDirection: request.SortDirection
                );
            }
            else
            {
                // Use standard search for backward compatibility
                (tickets, totalCount) = await _ticketRepository.GetAllAsync(
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
            }

            stopwatch.Stop();

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
                SlaStatus = CalculateSlaStatus(ticket.ExpectedResolutionDate, ticket.ActualResolutionDate, ticket.TicketStatusId),
                RelevanceScore = request.UseAdvancedSearch ? 0 : null // Will be populated from stored procedure in future enhancement
            }).ToList();

            var result = new PagedResult<TicketDto>
            {
                Items = ticketDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            // Log search analytics asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var searchCriteria = JsonSerializer.Serialize(new
                    {
                        request.CompanyId,
                        request.BranchId,
                        request.AssigneeId,
                        request.RequesterId,
                        request.StatusId,
                        request.StatusIds,
                        request.PriorityId,
                        request.PriorityIds,
                        request.TypeId,
                        request.TypeIds,
                        request.CategoryId,
                        request.CategoryIds,
                        request.CreatedFrom,
                        request.CreatedTo,
                        request.DueFrom,
                        request.DueTo,
                        request.SlaStatus,
                        request.SortBy,
                        request.SortDirection,
                        request.UseAdvancedSearch
                    });

                    await _searchAnalyticsRepository.LogSearchAsync(new SysSearchAnalytics
                    {
                        UserId = 0, // Will be set from HTTP context in controller
                        SearchTerm = request.SearchTerm,
                        SearchCriteria = searchCriteria,
                        FilterLogic = request.FilterLogic,
                        ResultCount = totalCount,
                        ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        CompanyId = request.CompanyId,
                        BranchId = request.BranchId,
                        SearchDate = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log search analytics (non-critical)");
                }
            }, cancellationToken);

            _logger.LogInformation("Retrieved {Count} tickets out of {TotalCount} total tickets in {ElapsedMs}ms", 
                ticketDtos.Count, totalCount, stopwatch.ElapsedMilliseconds);

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