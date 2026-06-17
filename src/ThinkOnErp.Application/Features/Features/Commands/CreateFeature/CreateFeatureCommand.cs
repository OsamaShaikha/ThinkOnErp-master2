using MediatR;

namespace ThinkOnErp.Application.Features.Features.Commands.CreateFeature;

public class CreateFeatureCommand : IRequest<long>
{
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string FeatureNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public int DisplayOrder { get; set; }
    public byte[]? IconFile { get; set; }
    public string? IconFileName { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
