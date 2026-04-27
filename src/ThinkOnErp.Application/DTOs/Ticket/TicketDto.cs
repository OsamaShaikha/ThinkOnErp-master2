namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Data transfer object for ticket information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class TicketDto
{
    /// <summary>
    /// Unique identifier for the ticket
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Arabic title of the ticket
    /// </summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>
    /// English title of the ticket
    /// </summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the ticket
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to SYS_COMPANY table
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Company name for display
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Branch name for display
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table (requester)
    /// </summary>
    public Int64 RequesterId { get; set; }

    /// <summary>
    /// Requester name for display
    /// </summary>
    public string? RequesterName { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table (assignee)
    /// </summary>
    public Int64? AssigneeId { get; set; }

    /// <summary>
    /// Assignee name for display
    /// </summary>
    public string? AssigneeName { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_TYPE table
    /// </summary>
    public Int64 TicketTypeId { get; set; }

    /// <summary>
    /// Ticket type name for display
    /// </summary>
    public string? TicketTypeName { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_STATUS table
    /// </summary>
    public Int64 TicketStatusId { get; set; }

    /// <summary>
    /// Ticket status name for display
    /// </summary>
    public string? TicketStatusName { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_PRIORITY table
    /// </summary>
    public Int64 TicketPriorityId { get; set; }

    /// <summary>
    /// Ticket priority name for display
    /// </summary>
    public string? TicketPriorityName { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_CATEGORY table
    /// </summary>
    public Int64? TicketCategoryId { get; set; }

    /// <summary>
    /// Ticket category name for display
    /// </summary>
    public string? TicketCategoryName { get; set; }

    /// <summary>
    /// Expected resolution date based on SLA
    /// </summary>
    public DateTime? ExpectedResolutionDate { get; set; }

    /// <summary>
    /// Actual resolution date when resolved
    /// </summary>
    public DateTime? ActualResolutionDate { get; set; }

    /// <summary>
    /// Indicates if the ticket is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    /// Number of comments on this ticket
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Number of attachments on this ticket
    /// </summary>
    public int AttachmentCount { get; set; }

    /// <summary>
    /// SLA compliance status
    /// </summary>
    public string SlaStatus { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score for search results (0-100+)
    /// Only populated when using advanced search with relevance scoring
    /// </summary>
    public int? RelevanceScore { get; set; }
}