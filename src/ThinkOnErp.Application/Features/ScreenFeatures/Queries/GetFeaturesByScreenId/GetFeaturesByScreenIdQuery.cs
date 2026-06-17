using MediatR;
using ThinkOnErp.Application.DTOs.Feature;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Queries.GetFeaturesByScreenId;

public class GetFeaturesByScreenIdQuery : IRequest<List<FeatureDto>>
{
    public long ScreenId { get; set; }
}
