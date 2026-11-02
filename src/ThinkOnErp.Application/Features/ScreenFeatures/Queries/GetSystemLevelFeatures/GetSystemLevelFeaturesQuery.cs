using MediatR;
using ThinkOnErp.Application.DTOs.Feature;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Queries.GetSystemLevelFeatures;

public class GetSystemLevelFeaturesQuery : IRequest<List<FeatureDto>>
{
    public long SystemId { get; set; }

    public GetSystemLevelFeaturesQuery() { }

    public GetSystemLevelFeaturesQuery(long systemId)
    {
        SystemId = systemId;
    }
}
