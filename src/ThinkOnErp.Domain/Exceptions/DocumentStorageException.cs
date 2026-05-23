namespace ThinkOnErp.Domain.Exceptions;

public class DocumentStorageException : DomainException
{
    public string FilePath { get; }

    public DocumentStorageException(string filePath, string message)
        : base($"Document storage error for '{filePath}': {message}", "DOC_STORAGE_ERROR")
    {
        FilePath = filePath;
        AddContext("FilePath", filePath);
    }

    public DocumentStorageException(string filePath, string message, Exception innerException)
        : base($"Document storage error for '{filePath}': {message}", "DOC_STORAGE_ERROR", innerException)
    {
        FilePath = filePath;
        AddContext("FilePath", filePath);
    }
}
