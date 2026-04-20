using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;

public class DeleteSuperAdminCommandHandler : IRequestHandler<DeleteSuperAdminCommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public DeleteSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        var rowsAffected = await _repository.DeleteAsync(request.SuperAdminId);
        return rowsAffected > 0;
    }
}
