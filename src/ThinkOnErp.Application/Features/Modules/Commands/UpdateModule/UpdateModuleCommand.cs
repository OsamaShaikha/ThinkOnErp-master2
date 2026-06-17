using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Modules.Commands.UpdateModule;

public class UpdateModuleCommand : IRequest<long>
{
    public long ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleNameE { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionE { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
