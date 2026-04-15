using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetScreensBySystemId;

/// <summary>
/// Query to get all screens for a specific system.
/// </summary>
public class GetScreensBySystemIdQuery : IRequest<List<ScreenDto>>
{
    /// <summary>
    /// System ID
    /// </summary>
    public Int64 SystemId { get; set; }
}
