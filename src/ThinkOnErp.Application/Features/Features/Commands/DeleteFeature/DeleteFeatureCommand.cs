using MediatR;

namespace ThinkOnErp.Application.Features.Features.Commands.DeleteFeature;

public class DeleteFeatureCommand : IRequest<long>
{
    public long FeatureId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
