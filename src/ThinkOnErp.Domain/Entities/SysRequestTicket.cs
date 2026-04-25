namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a request ticket entity in the ERP system.
/// Includes multilingual support, SLA tracking, and comprehensive audit trail.
/// Maps to the SYS_REQUEST_TICKET table in Oracle database.
/// </summary>
public class SysRequestTicket
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_REQUEST_TICKET sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Ticket title in Arabic (5-200 characters)
    /// </summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>
    /// Ticket title in English (5-200 characters)
    /// </summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>
    /// Detailed ticket description supporting rich text content (10-5000 characters)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to SYS_COMPANY table - the company submitting the ticket
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table - the branch submitting the ticket
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table - the user who submitted the ticket
    /// </summary>
    public Int64 RequesterId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table - the support staff assigned to handle the ticket (nullable)
    /// </summary>
    public Int64? AssigneeId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_TYPE table - categorizes the type of request
    /// </summary>
    public Int64 TicketTypeId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_STATUS table - current status of the ticket
    /// </summary>
    public Int64 TicketStatusId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_PRIORITY table - priority level of the ticket
    /// </summary>
    public Int64 TicketPriorityId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_CATEGORY table - optional additional categorization
    /// </summary>
    public Int64? TicketCategoryId { get; set; }

    /// <summary>
    /// Expected resolution date based on SLA targets and priority levels
    /// </summary>
    public DateTime? ExpectedResolutionDate { get; set; }

    /// <summary>
    /// Actual resolution date when ticket status changes to Resolved
    /// </summary>
    public DateTime? ActualResolutionDate { get; set; }

    /// <summary>
    /// Soft delete flag - true for active, false for deleted
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

    // Navigation properties
    /// <summary>
    /// Navigation property to the company that submitted the ticket
    /// </summary>
    public SysCompany? Company { get; set; }

    /// <summary>
    /// Navigation property to the branch that submitted the ticket
    /// </summary>
    public SysBranch? Branch { get; set; }

    /// <summary>
    /// Navigation property to the user who submitted the ticket
    /// </summary>
    public SysUser? Requester { get; set; }

    /// <summary>
    /// Navigation property to the support staff assigned to the ticket
    /// </summary>
    public SysUser? Assignee { get; set; }

    /// <summary>
    /// Navigation property to the ticket type
    /// </summary>
    public SysTicketType? TicketType { get; set; }

    /// <summary>
    /// Navigation property to the current ticket status
    /// </summary>
    public SysTicketStatus? TicketStatus { get; set; }

    /// <summary>
    /// Navigation property to the ticket priority
    /// </summary>
    public SysTicketPriority? TicketPriority { get; set; }

    /// <summary>
    /// Navigation property to the ticket category (optional)
    /// </summary>
    public SysTicketCategory? TicketCategory { get; set; }

    /// <summary>
    /// Navigation property to all comments associated with this ticket
    /// </summary>
    public List<SysTicketComment> Comments { get; set; } = new();

    /// <summary>
    /// Navigation property to all file attachments associated with this ticket
    /// </summary>
    public List<SysTicketAttachment> Attachments { get; set; } = new();

    // Business logic properties
    /// <summary>
    /// Indicates if the ticket is overdue based on SLA targets
    /// </summary>
    public bool IsOverdue => ExpectedResolutionDate.HasValue && 
                            DateTime.Now > ExpectedResolutionDate.Value && 
                            ActualResolutionDate == null;

    /// <summary>
    /// Indicates if the ticket is resolved (has actual resolution date)
    /// </summary>
    public bool IsResolved => ActualResolutionDate.HasValue;

    /// <summary>
    /// Calculates the age of the ticket in hours from creation
    /// </summary>
    public double AgeInHours => CreationDate.HasValue ? 
                               (DateTime.Now - CreationDate.Value).TotalHours : 0;

    /// <summary>
    /// Indicates if the ticket is within SLA compliance
    /// </summary>
    public bool IsWithinSla => !IsOverdue && (IsResolved ? 
                              (ActualResolutionDate <= ExpectedResolutionDate) : true);
}