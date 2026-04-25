namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Data transfer object for creating ticket attachments.
/// </summary>
public class CreateAttachmentDto
{
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
}