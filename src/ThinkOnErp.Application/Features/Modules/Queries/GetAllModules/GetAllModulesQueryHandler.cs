using MediatR;
using ThinkOnErp.Application.DTOs.Module;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Modules.Queries.GetAllModules;

public class GetAllModulesQueryHandler : IRequestHandler<GetAllModulesQuery, List<ModuleDto>>
{
    private readonly ISystemRepository _systemRepository;

    public GetAllModulesQueryHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<List<ModuleDto>> Handle(GetAllModulesQuery request, CancellationToken cancellationToken)
    {
        var systems = await _systemRepository.GetAllSystemsAsync();

        return systems.Select(s => new ModuleDto
        {
            Id = s.Id,
            ModuleCode = s.SystemCode,
            ModuleName = s.SystemName,
            ModuleNameE = s.SystemNameE,
            Description = s.Description,
            DescriptionE = s.DescriptionE,
            Icon = s.Icon,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive,
            CreationUser = s.CreationUser,
            CreationDate = s.CreationDate,
            UpdateUser = s.UpdateUser,
            UpdateDate = s.UpdateDate
        }).ToList();
    }
}
