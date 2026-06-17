using MediatR;
using ThinkOnErp.Application.DTOs.Module;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Modules.Queries.GetModuleById;

public class GetModuleByIdQueryHandler : IRequestHandler<GetModuleByIdQuery, ModuleDto?>
{
    private readonly ISystemRepository _systemRepository;

    public GetModuleByIdQueryHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<ModuleDto?> Handle(GetModuleByIdQuery request, CancellationToken cancellationToken)
    {
        var system = await _systemRepository.GetSystemByIdAsync(request.ModuleId);
        if (system == null)
            return null;

        return new ModuleDto
        {
            Id = system.Id,
            ModuleCode = system.SystemCode,
            ModuleName = system.SystemName,
            ModuleNameE = system.SystemNameE,
            Description = system.Description,
            DescriptionE = system.DescriptionE,
            Icon = system.Icon,
            DisplayOrder = system.DisplayOrder,
            IsActive = system.IsActive,
            CreationUser = system.CreationUser,
            CreationDate = system.CreationDate,
            UpdateUser = system.UpdateUser,
            UpdateDate = system.UpdateDate
        };
    }
}
