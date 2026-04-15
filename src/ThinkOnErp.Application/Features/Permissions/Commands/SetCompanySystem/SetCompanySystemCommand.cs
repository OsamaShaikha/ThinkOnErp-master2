using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetCompanySystem;

/// <summary>
/// Command to set system access for a company (allow or block).
/// </summary>
public class SetCompanySystemCommand : IRequest<Unit>
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
    /// True to allow, false to block
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Super Admin ID who is granting/revoking
    /// </summary>
    public Int64? GrantedBy { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Username for audit
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
