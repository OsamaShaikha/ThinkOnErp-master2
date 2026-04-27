namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a file attachment exceeds the maximum allowed size.
/// </summary>
public class AttachmentSizeExceededException : DomainException
{
    public long FileSize { get; }
    public long MaxSize { get; }

    public AttachmentSizeExceededException(long fileSize, long maxSize) 
        : base($"File size {fileSize} bytes exceeds maximum allowed size of {maxSize} bytes", "ATTACHMENT_SIZE_EXCEEDED")
    {
        FileSize = fileSize;
        MaxSize = maxSize;
        AddContext("FileSize", fileSize);
        AddContext("MaxSize", maxSize);
    }
}
