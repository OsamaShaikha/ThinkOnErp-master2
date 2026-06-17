using MediatR;
using ThinkOnErp.Application.DTOs.Screen;

namespace ThinkOnErp.Application.Features.Screens.Queries.GetScreensBySystemId;

public class GetScreensBySystemIdQuery : IRequest<List<ScreenDto>>
{
    public long SystemId { get; set; }
}
