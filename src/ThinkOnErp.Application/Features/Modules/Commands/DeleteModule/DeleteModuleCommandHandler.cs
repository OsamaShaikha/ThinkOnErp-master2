using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Modules.Commands.DeleteModule;

public class DeleteModuleCommandHandler : IRequestHandler<DeleteModuleCommand, long>
{
    private readonly ISystemRepository _systemRepository;

    public DeleteModuleCommandHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<long> Handle(DeleteModuleCommand request, CancellationToken cancellationToken)
    {
        var system = await _systemRepository.GetSystemByIdAsync(request.ModuleId);
        if (system == null)
            return 0;

        await _systemRepository.DeleteSystemAsync(request.ModuleId, request.UpdateUser);
        return 1;
    }
}
