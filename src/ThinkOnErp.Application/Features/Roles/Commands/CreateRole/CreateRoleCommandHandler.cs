using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Commands.CreateRole;

/// <summary>
/// Handler for CreateRoleCommand.
/// Creates a new role in the database and returns the generated ID.
/// </summary>
public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, decimal>
{
    private readonly IRoleRepository _roleRepository;

    public CreateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<decimal> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = new SysRole
        {
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            Note = request.Note,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        var newId = await _roleRepository.CreateAsync(role);
        return newId;
    }
}
