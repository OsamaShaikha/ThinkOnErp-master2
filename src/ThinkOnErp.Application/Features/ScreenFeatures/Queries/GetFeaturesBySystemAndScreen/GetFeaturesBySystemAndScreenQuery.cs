using MediatR;
using ThinkOnErp.Application.DTOs.Feature;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Queries.GetFeaturesBySystemAndScreen;

public class GetFeaturesBySystemAndScreenQuery : IRequest<List<FeatureDto>>
{
    public long SystemId { get; set; }
    public long? ScreenId { get; set; }

    public GetFeaturesBySystemAndScreenQuery() { }

    public GetFeaturesBySystemAndScreenQuery(long systemId, long? screenId)
    {
        SystemId = systemId;
        ScreenId = screenId;
    }
}
