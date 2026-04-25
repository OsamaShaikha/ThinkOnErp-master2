namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Data transfer object for ticket attachment information.
/// </summary>
public class TicketAttachmentDto
{
    /// <summary>
    /// Unique identifier for the attachment
    /// </summary>
    public Int64 AttachmentId { get; set; }

    /// <summary>
    /// Foreign key to SYS_REQUEST_TICKET table
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Original filename of the attachment
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public Int64 FileSize { get; set; }

    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Username of the user who uploaded this attachment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the user who uploaded this attachment
    /// </summary>
    public string? CreationUserName { get; set; }

    /// <summary>
    /// Timestamp when the attachment was uploaded
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Download URL for the attachment
    /// </summary>
    public string? DownloadUrl { get; set; }
}