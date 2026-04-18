using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranchLogo;

/// <summary>
/// Command to update a branch logo.
/// Handles both uploading new logos and deleting existing logos (by passing empty array).
/// </summary>
public class UpdateBranchLogoCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the branch
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Logo image as byte array. Pass empty array to delete existing logo.
    /// </summary>
    public byte[] Logo { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Username of the user updating the logo
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}