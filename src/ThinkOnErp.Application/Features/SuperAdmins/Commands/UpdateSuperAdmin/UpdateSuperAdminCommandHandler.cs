using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;

public class UpdateSuperAdminCommandHandler : IRequestHandler<UpdateSuperAdminCommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public UpdateSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        superAdmin.NameLocal = request.NameLocal;
        superAdmin.NameEn = request.NameEn;
        superAdmin.Email = request.Email;
        superAdmin.Phone = request.Phone;
        if (!string.IsNullOrEmpty(request.PinHash))
        {
            superAdmin.PinHash = request.PinHash;
        }
        superAdmin.UpdateUser = request.UpdateUser;
        superAdmin.UpdateDate = DateTime.UtcNow;

        var rowsAffected = await _repository.UpdateAsync(superAdmin);
        return rowsAffected > 0;
    }
}
