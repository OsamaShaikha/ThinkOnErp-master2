using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchLogo;

/// <summary>
/// Query to retrieve a branch logo by branch ID.
/// Returns the logo as a byte array or null if not found.
/// </summary>
public class GetBranchLogoQuery : IRequest<byte[]?>
{
    /// <summary>
    /// Unique identifier of the branch
    /// </summary>
    public Int64 BranchId { get; set; }
}