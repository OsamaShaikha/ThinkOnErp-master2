using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;

/// <summary>
/// Handler for DeleteRoleCommand.
/// Performs soft delete by setting IS_ACTIVE to false.
/// </summary>
public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, int>
{
    private readonly IRoleRepository _roleRepository;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<int> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var rowsAffected = await _roleRepository.DeleteAsync(request.RowId);
        return rowsAffected;
    }
}
