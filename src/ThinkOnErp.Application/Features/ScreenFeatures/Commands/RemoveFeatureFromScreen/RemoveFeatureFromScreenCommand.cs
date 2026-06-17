using MediatR;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.RemoveFeatureFromScreen;

public class RemoveFeatureFromScreenCommand : IRequest<bool>
{
    public long ScreenId { get; set; }
    public long FeatureId { get; set; }
}
