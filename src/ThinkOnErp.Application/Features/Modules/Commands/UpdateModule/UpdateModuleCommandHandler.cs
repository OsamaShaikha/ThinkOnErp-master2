using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Modules.Commands.UpdateModule;

public class UpdateModuleCommandHandler : IRequestHandler<UpdateModuleCommand, long>
{
    private readonly ISystemRepository _systemRepository;

    public UpdateModuleCommandHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<long> Handle(UpdateModuleCommand request, CancellationToken cancellationToken)
    {
        var system = await _systemRepository.GetSystemByIdAsync(request.ModuleId);
        if (system == null)
            return 0;

        system.SystemCode = request.ModuleCode;
        system.SystemName = request.ModuleName;
        system.SystemNameE = request.ModuleNameE;
        system.Description = request.Description;
        system.DescriptionE = request.DescriptionE;
        system.Icon = request.Icon;
        system.DisplayOrder = request.DisplayOrder;
        system.UpdateUser = request.UpdateUser;
        system.UpdateDate = DateTime.UtcNow;

        await _systemRepository.UpdateSystemAsync(system);
        return 1;
    }
}
