using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;

/// <summary>
/// Command for uploading a file attachment to a ticket.
/// </summary>
public class UploadAttachmentCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket to attach the file to
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Original filename of the attachment
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Base64 encoded file content
    /// </summary>
    public string FileContent { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Username of the user uploading the attachment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}