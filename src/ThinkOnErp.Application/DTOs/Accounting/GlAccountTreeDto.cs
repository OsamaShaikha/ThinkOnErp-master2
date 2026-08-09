namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class GlAccountTreeDto : GlAccountDto
{
    public List<GlAccountTreeDto> Children { get; set; } = new();
}
