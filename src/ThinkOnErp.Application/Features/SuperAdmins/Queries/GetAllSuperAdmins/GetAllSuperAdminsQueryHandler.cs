using MediatR;
using ThinkOnErp.Application.DTOs.SuperAdmin;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetAllSuperAdmins;

public class GetAllSuperAdminsQueryHandler : IRequestHandler<GetAllSuperAdminsQuery, List<SuperAdminDto>>
{
    private readonly ISuperAdminRepository _repository;

    public GetAllSuperAdminsQueryHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SuperAdminDto>> Handle(GetAllSuperAdminsQuery request, CancellationToken cancellationToken)
    {
        var superAdmins = await _repository.GetAllAsync();

        return superAdmins.Select(sa => new SuperAdminDto
        {
            SuperAdminId = sa.RowId,
            NameAr = sa.RowDesc,
            NameEn = sa.RowDescE,
            UserName = sa.UserName,
            Email = sa.Email,
            Phone = sa.Phone,
            TwoFaEnabled = sa.TwoFaEnabled,
            IsActive = sa.IsActive,
            LastLoginDate = sa.LastLoginDate,
            CreationUser = sa.CreationUser,
            CreationDate = sa.CreationDate,
            UpdateUser = sa.UpdateUser,
            UpdateDate = sa.UpdateDate
        }).ToList();
    }
}
