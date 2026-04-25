using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTickets;

/// <summary>
/// Query for retrieving tickets with filtering, sorting, and pagination.
/// </summary>
public class GetTicketsQuery : IRequest<PagedResult<TicketDto>>
{
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Search term for full-text search across titles and descriptions
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by company ID
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Filter by branch ID
    /// </summary>
    public Int64? BranchId { get; set; }

    /// <summary>
    /// Filter by requester ID
    /// </summary>
    public Int64? RequesterId { get; set; }

    /// <summary>
    /// Filter by assignee ID
    /// </summary>
    public Int64? AssigneeId { get; set; }

    /// <summary>
    /// Filter by ticket status ID
    /// </summary>
    public Int64? StatusId { get; set; }

    /// <summary>
    /// Filter by ticket priority ID
    /// </summary>
    public Int64? PriorityId { get; set; }

    /// <summary>
    /// Filter by ticket type ID
    /// </summary>
    public Int64? TypeId { get; set; }

    /// <summary>
    /// Filter by ticket category ID
    /// </summary>
    public Int64? CategoryId { get; set; }

    /// <summary>
    /// Filter by creation date from
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Filter by creation date to
    /// </summary>
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// Filter by expected resolution date from
    /// </summary>
    public DateTime? DueFrom { get; set; }

    /// <summary>
    /// Filter by expected resolution date to
    /// </summary>
    public DateTime? DueTo { get; set; }

    /// <summary>
    /// Filter by SLA compliance status (OnTime, AtRisk, Overdue)
    /// </summary>
    public string? SlaStatus { get; set; }

    /// <summary>
    /// Sort field (CreationDate, Priority, Status, DueDate, etc.)
    /// </summary>
    public string SortBy { get; set; } = "CreationDate";

    /// <summary>
    /// Sort direction (ASC or DESC)
    /// </summary>
    public string SortDirection { get; set; } = "DESC";

    /// <summary>
    /// Include only active tickets (default true)
    /// </summary>
    public bool IncludeActive { get; set; } = true;

    /// <summary>
    /// Include inactive tickets (default false)
    /// </summary>
    public bool IncludeInactive { get; set; } = false;
}