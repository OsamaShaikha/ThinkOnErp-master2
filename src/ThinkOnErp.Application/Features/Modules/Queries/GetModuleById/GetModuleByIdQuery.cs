using MediatR;
using ThinkOnErp.Application.DTOs.Module;

namespace ThinkOnErp.Application.Features.Modules.Queries.GetModuleById;

public class GetModuleByIdQuery : IRequest<ModuleDto?>
{
    public long ModuleId { get; set; }
}
