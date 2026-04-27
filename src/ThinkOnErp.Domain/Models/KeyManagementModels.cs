namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Metadata for an encryption or signing key.
/// </summary>
public class KeyMetadata
{
    public string KeyId { get; set; } = null!;
    public KeyType KeyType { get; set; }
    public string KeyValue { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Type of cryptographic key.
/// </summary>
public enum KeyType
{
    Encryption,
    Signing
}

/// <summary>
/// Result of key validation.
/// </summary>
public class KeyValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}
