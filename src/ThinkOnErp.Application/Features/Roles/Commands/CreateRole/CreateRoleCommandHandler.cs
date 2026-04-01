using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Commands.CreateRole;

/// <summary>
/// Handler for CreateRoleCommand.
/// Creates a new role in the database and returns the generated ID.
/// </summary>
public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Int64>
{
    private readonly IRoleRepository _roleRepository;

    public CreateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<Int64> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = new SysRole
        {
            RowDesc = request.RoleNameAr,
            RowDescE = request.RoleNameEn,
            Note = request.Note,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        var newId = await _roleRepository.CreateAsync(role);
        return newId;
    }
}
