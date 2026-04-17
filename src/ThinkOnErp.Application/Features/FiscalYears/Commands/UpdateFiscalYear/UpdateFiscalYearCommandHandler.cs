using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.UpdateFiscalYear;

public class UpdateFiscalYearCommandHandler : IRequestHandler<UpdateFiscalYearCommand, Int64>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public UpdateFiscalYearCommandHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<Int64> Handle(UpdateFiscalYearCommand request, CancellationToken cancellationToken)
    {
        var fiscalYear = new SysFiscalYear
        {
            RowId = request.FiscalYearId,
            CompanyId = request.CompanyId,
            FiscalYearCode = request.FiscalYearCode,
            RowDesc = request.FiscalYearNameAr,
            RowDescE = request.FiscalYearNameEn,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsClosed = request.IsClosed,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _fiscalYearRepository.UpdateAsync(fiscalYear);
    }
}
