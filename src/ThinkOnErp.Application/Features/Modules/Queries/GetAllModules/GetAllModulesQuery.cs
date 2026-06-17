using MediatR;
using ThinkOnErp.Application.DTOs.Module;

namespace ThinkOnErp.Application.Features.Modules.Queries.GetAllModules;

public class GetAllModulesQuery : IRequest<List<ModuleDto>>
{
}
