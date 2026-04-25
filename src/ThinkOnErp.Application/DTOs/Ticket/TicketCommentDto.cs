namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Data transfer object for ticket comment information.
/// </summary>
public class TicketCommentDto
{
    /// <summary>
    /// Unique identifier for the comment
    /// </summary>
    public Int64 CommentId { get; set; }

    /// <summary>
    /// Foreign key to SYS_REQUEST_TICKET table
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Comment text content
    /// </summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is an internal comment (visible only to admins)
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Username of the user who created this comment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the user who created this comment
    /// </summary>
    public string? CreationUserName { get; set; }

    /// <summary>
    /// Timestamp when the comment was created
    /// </summary>
    public DateTime? CreationDate { get; set; }
}