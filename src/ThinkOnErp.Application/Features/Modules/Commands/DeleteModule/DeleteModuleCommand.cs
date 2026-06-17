using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Modules.Commands.DeleteModule;

public class DeleteModuleCommand : IRequest<long>
{
    public long ModuleId { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
