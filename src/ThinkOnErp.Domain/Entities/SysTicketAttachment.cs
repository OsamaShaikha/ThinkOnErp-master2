namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a file attachment associated with a ticket.
/// Stores files as binary data with metadata and security validation.
/// Maps to the SYS_TICKET_ATTACHMENT table in Oracle database.
/// </summary>
public class SysTicketAttachment
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_ATTACHMENT sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_REQUEST_TICKET table - the ticket this attachment belongs to
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Original filename with extension
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public Int64 FileSize { get; set; }

    /// <summary>
    /// MIME type of the file (e.g., application/pdf, image/jpeg)
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File content stored as binary data (BLOB in database)
    /// </summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Username of the user who uploaded this attachment
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the attachment was uploaded
    /// </summary>
    public DateTime? CreationDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to the ticket this attachment belongs to
    /// </summary>
    public SysRequestTicket? Ticket { get; set; }

    // Business logic properties
    /// <summary>
    /// Maximum allowed file size in bytes (10MB)
    /// </summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum number of attachments per ticket
    /// </summary>
    public const int MaxAttachmentsPerTicket = 5;

    /// <summary>
    /// Allowed file extensions for security
    /// </summary>
    public static readonly string[] AllowedFileExtensions = 
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", 
        ".jpg", ".jpeg", ".png", ".txt"
    };

    /// <summary>
    /// Allowed MIME types for security validation
    /// </summary>
    public static readonly string[] AllowedMimeTypes = 
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/jpeg",
        "image/jpg", 
        "image/png",
        "text/plain"
    };

    /// <summary>
    /// Gets the file extension from the filename
    /// </summary>
    public string FileExtension => Path.GetExtension(FileName).ToLowerInvariant();

    /// <summary>
    /// Indicates if the file size is within allowed limits
    /// </summary>
    public bool IsFileSizeValid => FileSize > 0 && FileSize <= MaxFileSizeBytes;

    /// <summary>
    /// Indicates if the file extension is allowed
    /// </summary>
    public bool IsFileExtensionValid => AllowedFileExtensions.Contains(FileExtension);

    /// <summary>
    /// Indicates if the MIME type is allowed
    /// </summary>
    public bool IsMimeTypeValid => AllowedMimeTypes.Contains(MimeType.ToLowerInvariant());

    /// <summary>
    /// Indicates if the attachment passes all validation rules
    /// </summary>
    public bool IsValid => IsFileSizeValid && IsFileExtensionValid && IsMimeTypeValid;

    /// <summary>
    /// Gets a human-readable file size string
    /// </summary>
    public string GetFormattedFileSize()
    {
        if (FileSize < 1024)
            return $"{FileSize} B";
        else if (FileSize < 1024 * 1024)
            return $"{FileSize / 1024.0:F1} KB";
        else
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
    }

    /// <summary>
    /// Validates if the file content matches the declared MIME type
    /// </summary>
    /// <returns>True if content matches MIME type, false otherwise</returns>
    public bool ValidateContentType()
    {
        if (FileContent == null || FileContent.Length == 0)
            return false;

        // Basic file signature validation
        var fileSignature = FileContent.Take(8).ToArray();
        
        return MimeType.ToLowerInvariant() switch
        {
            "application/pdf" => fileSignature.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 }), // %PDF
            "image/jpeg" or "image/jpg" => fileSignature.Take(3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            "image/png" => fileSignature.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => true // For other types, rely on extension validation
        };
    }
}