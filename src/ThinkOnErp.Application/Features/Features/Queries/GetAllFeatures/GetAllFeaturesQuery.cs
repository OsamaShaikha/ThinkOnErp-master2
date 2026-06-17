using MediatR;
using ThinkOnErp.Application.DTOs.Feature;

namespace ThinkOnErp.Application.Features.Features.Queries.GetAllFeatures;

public class GetAllFeaturesQuery : IRequest<List<FeatureDto>>
{
}
