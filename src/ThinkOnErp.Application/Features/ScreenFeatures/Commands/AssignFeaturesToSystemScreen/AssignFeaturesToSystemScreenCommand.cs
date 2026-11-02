using MediatR;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToSystemScreen;

public class AssignFeaturesToSystemScreenCommand : IRequest<bool>
{
    public long SystemId { get; set; }
    public long? ScreenId { get; set; }
    public List<long> FeatureIds { get; set; } = new();
    public string Username { get; set; } = string.Empty;
}
