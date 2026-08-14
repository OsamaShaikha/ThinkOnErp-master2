using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class GlAccountTreeDto : GlAccountDto
{
    [JsonPropertyOrder(100)]
    public List<GlAccountTreeDto> Children { get; set; } = new();
}
