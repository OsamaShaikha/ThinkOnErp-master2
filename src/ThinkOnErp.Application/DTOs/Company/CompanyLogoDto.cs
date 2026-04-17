namespace ThinkOnErp.Application.DTOs.Company;

/// <summary>
/// Data transfer object for company logo operations.
/// Used for logo upload/download operations.
/// </summary>
public class CompanyLogoDto
{
    /// <summary>
    /// Company ID
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Logo image as base64 encoded string
    /// </summary>
    public string? LogoBase64 { get; set; }

    /// <summary>
    /// Logo file name
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Logo content type (e.g., image/png, image/jpeg)
    /// </summary>
    public string? ContentType { get; set; }
}
