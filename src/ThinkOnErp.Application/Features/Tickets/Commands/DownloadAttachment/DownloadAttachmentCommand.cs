using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.DownloadAttachment;

/// <summary>
/// Command for downloading a ticket attachment with security checks.
/// </summary>
public class DownloadAttachmentCommand : IRequest<DownloadAttachmentResult>
{
    /// <summary>
    /// Unique identifier of the ticket
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Unique identifier of the attachment
    /// </summary>
    public Int64 AttachmentId { get; set; }

    /// <summary>
    /// Username of the user requesting the download
    /// </summary>
    public string RequestingUser { get; set; } = string.Empty;

    public DownloadAttachmentCommand(Int64 ticketId, Int64 attachmentId, string requestingUser)
    {
        TicketId = ticketId;
        AttachmentId = attachmentId;
        RequestingUser = requestingUser;
    }
}

/// <summary>
/// Result of attachment download operation.
/// </summary>
public class DownloadAttachmentResult
{
    /// <summary>
    /// File content as byte array
    /// </summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Original filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public Int64 FileSize { get; set; }
}