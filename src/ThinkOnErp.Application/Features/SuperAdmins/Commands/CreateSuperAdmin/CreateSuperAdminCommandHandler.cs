using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.CreateSuperAdmin;

public class CreateSuperAdminCommandHandler : IRequestHandler<CreateSuperAdminCommand, Int64>
{
    private readonly ISuperAdminRepository _repository;

    public CreateSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<Int64> Handle(CreateSuperAdminCommand request, CancellationToken cancellationToken)
    {
        // Check if username already exists
        var existing = await _repository.GetByUsernameAsync(request.UserName);
        if (existing != null)
        {
            throw new InvalidOperationException($"Username '{request.UserName}' already exists");
        }

        // Check if email already exists
        if (!string.IsNullOrEmpty(request.Email))
        {
            var existingEmail = await _repository.GetByEmailAsync(request.Email);
            if (existingEmail != null)
            {
                throw new InvalidOperationException($"Email '{request.Email}' already exists");
            }
        }

        var superAdmin = new SysSuperAdmin
        {
            RowDesc = request.NameAr,
            RowDescE = request.NameEn,
            UserName = request.UserName,
            Password = request.Password, // Will be hashed in API layer before reaching here
            Email = request.Email,
            Phone = request.Phone,
            TwoFaEnabled = false,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _repository.CreateAsync(superAdmin);
    }
}
