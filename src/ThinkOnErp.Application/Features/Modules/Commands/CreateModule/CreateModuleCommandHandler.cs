using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Modules.Commands.CreateModule;

public class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, long>
{
    private readonly ISystemRepository _systemRepository;

    public CreateModuleCommandHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<long> Handle(CreateModuleCommand request, CancellationToken cancellationToken)
    {
        var system = new SysSystem
        {
            SystemCode = request.ModuleCode,
            SystemName = request.ModuleName,
            SystemNameE = request.ModuleNameE,
            Description = request.Description,
            DescriptionE = request.DescriptionE,
            Icon = request.Icon,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _systemRepository.CreateSystemAsync(system);
    }
}
