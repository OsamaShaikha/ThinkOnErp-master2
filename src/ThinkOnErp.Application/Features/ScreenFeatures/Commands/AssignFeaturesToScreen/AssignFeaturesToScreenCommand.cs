using MediatR;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToScreen;

public class AssignFeaturesToScreenCommand : IRequest<bool>
{
    public long ScreenId { get; set; }
    public List<long> FeatureIds { get; set; } = new();
}
