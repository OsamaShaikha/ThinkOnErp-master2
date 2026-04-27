namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when an unsupported file type is uploaded.
/// </summary>
public class InvalidFileTypeException : DomainException
{
    public string FileName { get; }
    public string MimeType { get; }

    public InvalidFileTypeException(string fileName, string mimeType) 
        : base($"File type '{mimeType}' is not allowed for file '{fileName}'", "INVALID_FILE_TYPE")
    {
        FileName = fileName;
        MimeType = mimeType;
        AddContext("FileName", fileName);
        AddContext("MimeType", mimeType);
    }
}
