using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByCompany;

public class GetFiscalYearsByCompanyQueryHandler : IRequestHandler<GetFiscalYearsByCompanyQuery, List<FiscalYearDto>>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public GetFiscalYearsByCompanyQueryHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<List<FiscalYearDto>> Handle(GetFiscalYearsByCompanyQuery request, CancellationToken cancellationToken)
    {
        var fiscalYears = await _fiscalYearRepository.GetByCompanyIdAsync(request.CompanyId);

        return fiscalYears.Select(fy => new FiscalYearDto
        {
            FiscalYearId = fy.RowId,
            CompanyId = fy.CompanyId,
            FiscalYearCode = fy.FiscalYearCode,
            FiscalYearNameAr = fy.RowDesc,
            FiscalYearNameEn = fy.RowDescE,
            StartDate = fy.StartDate,
            EndDate = fy.EndDate,
            IsClosed = fy.IsClosed,
            IsActive = fy.IsActive,
            CreationUser = fy.CreationUser,
            CreationDate = fy.CreationDate,
            UpdateUser = fy.UpdateUser,
            UpdateDate = fy.UpdateDate
        }).ToList();
    }
}
