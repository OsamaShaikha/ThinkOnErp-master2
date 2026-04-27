namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for masking sensitive data in audit logs and other contexts.
/// Supports both JSON objects and plain text strings.
/// </summary>
public interface ISensitiveDataMasker
{
    /// <summary>
    /// Masks sensitive fields in a JSON string or plain text.
    /// If the input is valid JSON, masks sensitive fields recursively.
    /// If the input is plain text, masks common sensitive patterns.
    /// </summary>
    /// <param name="json">JSON string or plain text to process</param>
    /// <returns>String with sensitive fields masked</returns>
    string? MaskSensitiveFields(string? json);
    
    /// <summary>
    /// Masks sensitive data in plain text strings using pattern matching.
    /// Useful for masking sensitive data in non-JSON strings like URLs or query strings.
    /// </summary>
    /// <param name="text">Plain text to process</param>
    /// <returns>Text with sensitive patterns masked</returns>
    string? MaskSensitiveInPlainText(string? text);
    
    /// <summary>
    /// Truncates a string if it exceeds the maximum payload size.
    /// </summary>
    /// <param name="value">String to truncate</param>
    /// <returns>Truncated string with indicator if truncation occurred</returns>
    string? TruncateIfNeeded(string? value);
}
