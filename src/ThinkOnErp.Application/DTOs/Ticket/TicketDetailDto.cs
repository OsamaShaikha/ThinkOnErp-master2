namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Detailed data transfer object for ticket information with related entities.
/// Used for detailed ticket views including comments and attachments.
/// </summary>
public class TicketDetailDto : TicketDto
{
    /// <summary>
    /// List of comments associated with this ticket
    /// </summary>
    public List<TicketCommentDto> Comments { get; set; } = new();

    /// <summary>
    /// List of attachments associated with this ticket
    /// </summary>
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
}