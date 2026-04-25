using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for handling file attachments.
/// Provides validation, encoding/decoding, and security checks for file attachments.
/// </summary>
public class AttachmentService : IAttachmentService
{
    private readonly ILogger<AttachmentService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration keys
    private const string MaxFileSizeKey = "Attachments:MaxFileSizeBytes";
    private const string MaxAttachmentCountKey = "Attachments:MaxAttachmentCount";
    private const string AllowedExtensionsKey = "Attachments:AllowedExtensions";

    // Default values
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int DefaultMaxAttachmentCount = 5;

    // Allowed file types and their MIME types
    private static readonly Dictionary<string, string[]> AllowedFileTypes = new()
    {
        { ".pdf", new[] { "application/pdf" } },
        { ".doc", new[] { "application/msword" } },
        { ".docx", new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } },
        { ".xls", new[] { "application/vnd.ms-excel" } },
        { ".xlsx", new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
        { ".jpg", new[] { "image/jpeg" } },
        { ".jpeg", new[] { "image/jpeg" } },
        { ".png", new[] { "image/png" } },
        { ".txt", new[] { "text/plain" } }
    };

    // File signatures for content validation
    private static readonly Dictionary<string, byte[][]> FileSignatures = new()
    {
        { ".pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } }, // %PDF
        { ".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }, // JPEG
        { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }, // JPEG
        { ".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } }, // PNG
        { ".doc", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } }, // MS Office
        { ".docx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 } } }, // ZIP-based
        { ".xls", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } }, // MS Office
        { ".xlsx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 } } } // ZIP-based
    };

    public AttachmentService(
        ILogger<AttachmentService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsValidFileType(string fileName, string mimeType)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(mimeType))
        {
            _logger.LogWarning("File name or MIME type is null or empty");
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        if (!AllowedFileTypes.TryGetValue(extension, out var validMimeTypes))
        {
            _logger.LogWarning("File extension {Extension} is not allowed", extension);
            return false;
        }

        var isValidMimeType = validMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
        if (!isValidMimeType)
        {
            _logger.LogWarning("MIME type {MimeType} is not valid for extension {Extension}", mimeType, extension);
        }

        return isValidMimeType;
    }

    public bool IsValidFileSize(string base64Content)
    {
        if (string.IsNullOrEmpty(base64Content))
        {
            _logger.LogWarning("Base64 content is null or empty");
            return false;
        }

        try
        {
            // Calculate file size from Base64 string
            var base64Length = base64Content.Length;
            var paddingCount = base64Content.EndsWith("==") ? 2 : base64Content.EndsWith("=") ? 1 : 0;
            var fileSizeBytes = (base64Length * 3 / 4) - paddingCount;

            var maxSize = GetMaxFileSizeBytes();
            var isValid = fileSizeBytes <= maxSize;

            if (!isValid)
            {
                _logger.LogWarning("File size {FileSize} bytes exceeds maximum allowed size {MaxSize} bytes", 
                    fileSizeBytes, maxSize);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating file size from Base64 content");
            return false;
        }
    }

    public bool IsValidBase64Content(string base64Content)
    {
        if (string.IsNullOrEmpty(base64Content))
        {
            _logger.LogWarning("Base64 content is null or empty");
            return false;
        }

        try
        {
            // Remove any whitespace
            var cleanBase64 = base64Content.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
            
            // Check if length is valid (must be multiple of 4)
            if (cleanBase64.Length % 4 != 0)
            {
                _logger.LogWarning("Base64 content length is not a multiple of 4");
                return false;
            }

            // Try to convert to validate format
            Convert.FromBase64String(cleanBase64);
            return true;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid Base64 format");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Base64 content");
            return false;
        }
    }

    public async Task<bool> ValidateFileContentAsync(byte[] fileBytes, string mimeType, string fileName)
    {
        if (fileBytes == null || fileBytes.Length == 0)
        {
            _logger.LogWarning("File bytes are null or empty");
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Skip signature validation for text files
        if (extension == ".txt")
        {
            return await ValidateTextFileAsync(fileBytes);
        }

        // Validate file signature
        if (FileSignatures.TryGetValue(extension, out var signatures))
        {
            var isValidSignature = signatures.Any(signature => 
                fileBytes.Length >= signature.Length && 
                fileBytes.Take(signature.Length).SequenceEqual(signature));

            if (!isValidSignature)
            {
                _logger.LogWarning("File signature does not match expected signature for extension {Extension}", extension);
                return false;
            }
        }

        // Additional MIME type validation
        return IsValidFileType(fileName, mimeType);
    }

    public byte[] DecodeBase64Content(string base64Content)
    {
        if (!IsValidBase64Content(base64Content))
        {
            throw new ArgumentException("Invalid Base64 content format");
        }

        try
        {
            var cleanBase64 = base64Content.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
            return Convert.FromBase64String(cleanBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding Base64 content");
            throw new ArgumentException("Failed to decode Base64 content", ex);
        }
    }

    public string EncodeToBase64(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length == 0)
        {
            throw new ArgumentException("File bytes cannot be null or empty");
        }

        try
        {
            return Convert.ToBase64String(fileBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encoding bytes to Base64");
            throw new ArgumentException("Failed to encode bytes to Base64", ex);
        }
    }

    public long GetMaxFileSizeBytes()
    {
        return _configuration.GetValue<long>(MaxFileSizeKey, DefaultMaxFileSizeBytes);
    }

    public int GetMaxAttachmentCount()
    {
        return _configuration.GetValue<int>(MaxAttachmentCountKey, DefaultMaxAttachmentCount);
    }

    public string[] GetAllowedFileExtensions()
    {
        var configuredExtensions = _configuration.GetValue<string>(AllowedExtensionsKey);
        
        if (!string.IsNullOrEmpty(configuredExtensions))
        {
            return configuredExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(ext => ext.Trim().ToLowerInvariant())
                                     .ToArray();
        }

        return AllowedFileTypes.Keys.ToArray();
    }

    public string[] GetAllowedMimeTypes()
    {
        return AllowedFileTypes.Values.SelectMany(types => types).Distinct().ToArray();
    }

    #region Private Helper Methods

    private async Task<bool> ValidateTextFileAsync(byte[] fileBytes)
    {
        try
        {
            // Try to decode as UTF-8 text
            var text = System.Text.Encoding.UTF8.GetString(fileBytes);
            
            // Check if it contains valid text characters
            var hasValidText = text.All(c => char.IsControl(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || 
                                           char.IsLetterOrDigit(c) || char.IsSymbol(c));
            
            return await Task.FromResult(hasValidText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate text file content");
            return false;
        }
    }

    #endregion
}