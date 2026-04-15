namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for company system assignments.
/// </summary>
public class CompanySystemDto
{
    /// <summary>
    /// Company ID
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// System ID
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// System code
    /// </summary>
    public string SystemCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic system name
    /// </summary>
    public string SystemNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English system name
    /// </summary>
    public string SystemNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if system is allowed for this company
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Super Admin who granted/revoked access
    /// </summary>
    public Int64? GrantedBy { get; set; }

    /// <summary>
    /// Timestamp when granted
    /// </summary>
    public DateTime? GrantedDate { get; set; }

    /// <summary>
    /// Timestamp when revoked
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }
}
