using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetAllSystems;

/// <summary>
/// Query to get all active systems.
/// </summary>
public class GetAllSystemsQuery : IRequest<List<SystemDto>>
{
}
