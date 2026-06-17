using MediatR;

namespace ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;

public class CreateScreenCommand : IRequest<long>
{
    public long SystemId { get; set; }
    public long? ParentScreenId { get; set; }
    public string ScreenCode { get; set; } = string.Empty;
    public string ScreenName { get; set; } = string.Empty;
    public string ScreenNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public int DisplayOrder { get; set; }
    public byte[]? IconFile { get; set; }
    public string? IconFileName { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
