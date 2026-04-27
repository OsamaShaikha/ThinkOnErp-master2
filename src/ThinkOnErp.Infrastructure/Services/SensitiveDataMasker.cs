using System.Text.Json;
using System.Text.RegularExpressions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for masking sensitive data in audit logs.
/// Recursively processes JSON objects to mask configured sensitive fields.
/// Supports both exact field name matching and regex pattern matching.
/// </summary>
public class SensitiveDataMasker : ISensitiveDataMasker
{
    private readonly AuditLoggingOptions _options;
    private readonly HashSet<string> _sensitiveFields;
    private readonly List<Regex> _sensitiveFieldPatterns;

    public SensitiveDataMasker(IOptions<AuditLoggingOptions> options)
    {
        _options = options.Value;
        _sensitiveFields = new HashSet<string>(_options.SensitiveFields, StringComparer.OrdinalIgnoreCase);
        
        // Compile regex patterns for field matching
        _sensitiveFieldPatterns = new List<Regex>();
        foreach (var field in _options.SensitiveFields)
        {
            // Check if the field contains regex special characters
            if (IsRegexPattern(field))
            {
                try
                {
                    // Compile regex with case-insensitive matching
                    var regex = new Regex(field, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                    _sensitiveFieldPatterns.Add(regex);
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern, skip it
                }
            }
        }
    }
    
    /// <summary>
    /// Checks if a string contains regex special characters.
    /// </summary>
    private static bool IsRegexPattern(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?') || pattern.Contains('[') || 
               pattern.Contains(']') || pattern.Contains('(') || pattern.Contains(')') ||
               pattern.Contains('{') || pattern.Contains('}') || pattern.Contains('^') ||
               pattern.Contains('$') || pattern.Contains('.') || pattern.Contains('|') ||
               pattern.Contains('+') || pattern.Contains('\\');
    }

    /// <summary>
    /// Masks sensitive fields in a JSON string.
    /// </summary>
    /// <param name="json">JSON string to process</param>
    /// <returns>JSON string with sensitive fields masked</returns>
    public string? MaskSensitiveFields(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        try
        {
            using var document = JsonDocument.Parse(json);
            var maskedElement = MaskJsonElement(document.RootElement);
            return JsonSerializer.Serialize(maskedElement);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as plain string and mask patterns
            return MaskSensitiveInPlainText(json);
        }
    }
    
    /// <summary>
    /// Masks sensitive data in plain text strings using pattern matching.
    /// Useful for masking sensitive data in non-JSON strings like URLs or query strings.
    /// </summary>
    /// <param name="text">Plain text to process</param>
    /// <returns>Text with sensitive patterns masked</returns>
    public string? MaskSensitiveInPlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        var result = text;
        
        // Mask common sensitive patterns in plain text
        // Credit card numbers (13-19 digits with optional spaces/dashes)
        result = Regex.Replace(result, @"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{3,4}\b", _options.MaskingPattern, RegexOptions.IgnoreCase);
        
        // SSN patterns (XXX-XX-XXXX)
        result = Regex.Replace(result, @"\b\d{3}-\d{2}-\d{4}\b", _options.MaskingPattern, RegexOptions.IgnoreCase);
        
        // Email addresses (if email is in sensitive fields)
        if (_sensitiveFields.Contains("email"))
        {
            result = Regex.Replace(result, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", _options.MaskingPattern, RegexOptions.IgnoreCase);
        }
        
        // Bearer tokens in Authorization headers
        result = Regex.Replace(result, @"Bearer\s+[A-Za-z0-9\-._~+/]+=*", $"Bearer {_options.MaskingPattern}", RegexOptions.IgnoreCase);
        
        // API keys (common patterns like "apikey=...", "api_key=...", "key=...")
        result = Regex.Replace(result, @"(api[_\-]?key|key|secret)[\s]*=[\s]*[A-Za-z0-9\-._~+/]+", $"$1={_options.MaskingPattern}", RegexOptions.IgnoreCase);
        
        return result;
    }

    /// <summary>
    /// Recursively masks sensitive fields in a JsonElement.
    /// </summary>
    /// <param name="element">JsonElement to process</param>
    /// <returns>Masked object</returns>
    private object? MaskJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => MaskJsonObject(element),
            JsonValueKind.Array => MaskJsonArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Masks sensitive fields in a JSON object.
    /// </summary>
    /// <param name="element">JsonElement representing an object</param>
    /// <returns>Dictionary with masked sensitive fields</returns>
    private Dictionary<string, object?> MaskJsonObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            var propertyName = property.Name;
            var propertyValue = property.Value;

            if (IsSensitiveField(propertyName))
            {
                result[propertyName] = _options.MaskingPattern;
            }
            else
            {
                result[propertyName] = MaskJsonElement(propertyValue);
            }
        }

        return result;
    }

    /// <summary>
    /// Masks sensitive fields in a JSON array.
    /// </summary>
    /// <param name="element">JsonElement representing an array</param>
    /// <returns>List with masked sensitive fields</returns>
    private List<object?> MaskJsonArray(JsonElement element)
    {
        var result = new List<object?>();

        foreach (var item in element.EnumerateArray())
        {
            result.Add(MaskJsonElement(item));
        }

        return result;
    }

    /// <summary>
    /// Checks if a field name is considered sensitive.
    /// Supports both exact matching and regex pattern matching.
    /// </summary>
    /// <param name="fieldName">Field name to check</param>
    /// <returns>True if the field is sensitive</returns>
    private bool IsSensitiveField(string fieldName)
    {
        // First check exact match (faster)
        if (_sensitiveFields.Contains(fieldName))
            return true;
        
        // Then check regex patterns
        foreach (var pattern in _sensitiveFieldPatterns)
        {
            try
            {
                if (pattern.IsMatch(fieldName))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Regex timeout, skip this pattern
                continue;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Truncates a string if it exceeds the maximum payload size.
    /// </summary>
    /// <param name="value">String to truncate</param>
    /// <returns>Truncated string with indicator if truncation occurred</returns>
    public string? TruncateIfNeeded(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxPayloadSize)
            return value;

        var truncated = value.Substring(0, _options.MaxPayloadSize);
        return $"{truncated}... [TRUNCATED: {value.Length - _options.MaxPayloadSize} characters removed]";
    }
}