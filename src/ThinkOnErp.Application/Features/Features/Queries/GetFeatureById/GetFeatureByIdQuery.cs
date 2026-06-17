using MediatR;
using ThinkOnErp.Application.DTOs.Feature;

namespace ThinkOnErp.Application.Features.Features.Queries.GetFeatureById;

public class GetFeatureByIdQuery : IRequest<FeatureDto?>
{
    public long FeatureId { get; set; }
}
