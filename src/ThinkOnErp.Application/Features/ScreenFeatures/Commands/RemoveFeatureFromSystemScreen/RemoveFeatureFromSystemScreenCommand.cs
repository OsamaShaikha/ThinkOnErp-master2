using MediatR;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.RemoveFeatureFromSystemScreen;

public class RemoveFeatureFromSystemScreenCommand : IRequest<bool>
{
    public long SystemId { get; set; }
    public long? ScreenId { get; set; }
    public long FeatureId { get; set; }
}
