using MediatR;

namespace ThinkOnErp.Application.Features.Features.Commands.UpdateFeature;

public class UpdateFeatureCommand : IRequest<long>
{
    public long FeatureId { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string FeatureNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public int DisplayOrder { get; set; }
    public byte[]? IconFile { get; set; }
    public string? IconFileName { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
