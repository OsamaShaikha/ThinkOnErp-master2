using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Modules.Commands.CreateModule;

public class CreateModuleCommand : IRequest<long>
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    [JsonIgnore]
    public string CreationUser { get; set; } = string.Empty;
}
