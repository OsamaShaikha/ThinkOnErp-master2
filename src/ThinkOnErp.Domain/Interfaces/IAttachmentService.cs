namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for file attachment service.
/// Handles file validation, encoding/decoding, and security checks.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Validates file type based on filename and MIME type.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="mimeType">The MIME type</param>
    /// <returns>True if file type is allowed</returns>
    bool IsValidFileType(string fileName, string mimeType);

    /// <summary>
    /// Validates file size against configured limits.
    /// </summary>
    /// <param name="base64Content">Base64 encoded file content</param>
    /// <returns>True if file size is within limits</returns>
    bool IsValidFileSize(string base64Content);

    /// <summary>
    /// Validates Base64 content format.
    /// </summary>
    /// <param name="base64Content">Base64 content to validate</param>
    /// <returns>True if valid Base64 format</returns>
    bool IsValidBase64Content(string base64Content);

    /// <summary>
    /// Validates file content matches declared MIME type.
    /// </summary>
    /// <param name="fileBytes">File content as byte array</param>
    /// <param name="mimeType">Declared MIME type</param>
    /// <param name="fileName">File name</param>
    /// <returns>True if content matches MIME type</returns>
    Task<bool> ValidateFileContentAsync(byte[] fileBytes, string mimeType, string fileName);

    /// <summary>
    /// Converts Base64 string to byte array with validation.
    /// </summary>
    /// <param name="base64Content">Base64 encoded content</param>
    /// <returns>Byte array of file content</returns>
    byte[] DecodeBase64Content(string base64Content);

    /// <summary>
    /// Converts byte array to Base64 string.
    /// </summary>
    /// <param name="fileBytes">File content as byte array</param>
    /// <returns>Base64 encoded string</returns>
    string EncodeToBase64(byte[] fileBytes);

    /// <summary>
    /// Gets the maximum allowed file size in bytes.
    /// </summary>
    /// <returns>Maximum file size in bytes</returns>
    long GetMaxFileSizeBytes();

    /// <summary>
    /// Gets the maximum number of attachments allowed per ticket.
    /// </summary>
    /// <returns>Maximum attachment count</returns>
    int GetMaxAttachmentCount();

    /// <summary>
    /// Gets the list of allowed file extensions.
    /// </summary>
    /// <returns>Array of allowed file extensions</returns>
    string[] GetAllowedFileExtensions();

    /// <summary>
    /// Gets the list of allowed MIME types.
    /// </summary>
    /// <returns>Array of allowed MIME types</returns>
    string[] GetAllowedMimeTypes();
}