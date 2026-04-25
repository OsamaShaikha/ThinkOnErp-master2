namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a comment on a ticket for communication and progress tracking.
/// Supports internal/public visibility and maintains chronological order.
/// Maps to the SYS_TICKET_COMMENT table in Oracle database.
/// </summary>
public class SysTicketComment
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_COMMENT sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_REQUEST_TICKET table - the ticket this comment belongs to
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Comment text supporting rich text formatting (up to 2000 characters)
    /// </summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is an internal comment visible only to admin users
    /// False = public comment visible to requester and admin users
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Username of the user who created this comment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the comment was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to the ticket this comment belongs to
    /// </summary>
    public SysRequestTicket? Ticket { get; set; }

    // Business logic properties
    /// <summary>
    /// Indicates if this comment is visible to the ticket requester
    /// </summary>
    public bool IsVisibleToRequester => !IsInternal;

    /// <summary>
    /// Indicates if this comment is visible only to admin users
    /// </summary>
    public bool IsAdminOnly => IsInternal;

    /// <summary>
    /// Calculates the age of the comment in hours from creation
    /// </summary>
    public double AgeInHours => CreationDate.HasValue ? 
                               (DateTime.Now - CreationDate.Value).TotalHours : 0;

    /// <summary>
    /// Gets a truncated version of the comment text for display in lists
    /// </summary>
    /// <param name="maxLength">Maximum length of the truncated text</param>
    /// <returns>Truncated comment text with ellipsis if needed</returns>
    public string GetTruncatedText(int maxLength = 100)
    {
        if (string.IsNullOrEmpty(CommentText) || CommentText.Length <= maxLength)
            return CommentText;
        
        return CommentText.Substring(0, maxLength) + "...";
    }
}