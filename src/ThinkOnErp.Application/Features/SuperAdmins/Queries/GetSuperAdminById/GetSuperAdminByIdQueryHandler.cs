using MediatR;
using ThinkOnErp.Application.DTOs.SuperAdmin;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminById;

public class GetSuperAdminByIdQueryHandler : IRequestHandler<GetSuperAdminByIdQuery, SuperAdminDto?>
{
    private readonly ISuperAdminRepository _repository;

    public GetSuperAdminByIdQueryHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<SuperAdminDto?> Handle(GetSuperAdminByIdQuery request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);

        if (superAdmin == null)
            return null;

        return new SuperAdminDto
        {
            SuperAdminId = superAdmin.RowId,
            NameAr = superAdmin.RowDesc,
            NameEn = superAdmin.RowDescE,
            UserName = superAdmin.UserName,
            Email = superAdmin.Email,
            Phone = superAdmin.Phone,
            TwoFaEnabled = superAdmin.TwoFaEnabled,
            IsActive = superAdmin.IsActive,
            LastLoginDate = superAdmin.LastLoginDate,
            CreationUser = superAdmin.CreationUser,
            CreationDate = superAdmin.CreationDate,
            UpdateUser = superAdmin.UpdateUser,
            UpdateDate = superAdmin.UpdateDate
        };
    }
}
