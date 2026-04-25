using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearById;

public class GetFiscalYearByIdQueryHandler : IRequestHandler<GetFiscalYearByIdQuery, FiscalYearDto?>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public GetFiscalYearByIdQueryHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<FiscalYearDto?> Handle(GetFiscalYearByIdQuery request, CancellationToken cancellationToken)
    {
        var fiscalYear = await _fiscalYearRepository.GetByIdAsync(request.FiscalYearId);

        if (fiscalYear == null)
            return null;

        return new FiscalYearDto
        {
            FiscalYearId = fiscalYear.RowId,
            CompanyId = fiscalYear.CompanyId,
            BranchId = fiscalYear.BranchId,
            FiscalYearCode = fiscalYear.FiscalYearCode,
            FiscalYearNameAr = fiscalYear.RowDesc,
            FiscalYearNameEn = fiscalYear.RowDescE,
            StartDate = fiscalYear.StartDate,
            EndDate = fiscalYear.EndDate,
            IsClosed = fiscalYear.IsClosed,
            IsActive = fiscalYear.IsActive,
            CreationUser = fiscalYear.CreationUser,
            CreationDate = fiscalYear.CreationDate,
            UpdateUser = fiscalYear.UpdateUser,
            UpdateDate = fiscalYear.UpdateDate
        };
    }
}
