using MediatR;
using ThinkOnErp.Application.DTOs.Screen;

namespace ThinkOnErp.Application.Features.Screens.Queries.GetAllScreens;

public class GetAllScreensQuery : IRequest<List<ScreenDto>>
{
}
