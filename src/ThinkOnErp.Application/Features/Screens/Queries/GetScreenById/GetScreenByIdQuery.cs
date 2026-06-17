using MediatR;
using ThinkOnErp.Application.DTOs.Screen;

namespace ThinkOnErp.Application.Features.Screens.Queries.GetScreenById;

public class GetScreenByIdQuery : IRequest<ScreenDto?>
{
    public long ScreenId { get; set; }
}
