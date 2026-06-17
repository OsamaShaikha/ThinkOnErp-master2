using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Screens.Commands.DeleteScreen;

public class DeleteScreenCommand : IRequest<long>
{
    public long ScreenId { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
