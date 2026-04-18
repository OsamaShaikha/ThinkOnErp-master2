namespace ThinkOnErp.Application.DTOs.Branch;

/// <summary>
/// Data transfer object for branch logo operations.
/// Used for logo upload/download operations.
/// </summary>
public class BranchLogoDto
{
    /// <summary>
    /// Unique identifier for the branch
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Logo image as byte array
    /// </summary>
    public byte[] Logo { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Content type of the logo image (e.g., "image/jpeg", "image/png")
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Original filename of the logo
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Size of the logo in bytes
    /// </summary>
    public long Size => Logo.Length;
}