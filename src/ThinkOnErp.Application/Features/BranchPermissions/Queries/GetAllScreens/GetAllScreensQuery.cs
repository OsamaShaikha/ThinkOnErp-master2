using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllScreens;

public class GetAllScreensQuery : IRequest<List<ScreenDto>>
{
}
