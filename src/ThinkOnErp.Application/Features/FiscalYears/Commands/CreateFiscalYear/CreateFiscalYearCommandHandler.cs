using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.CreateFiscalYear;

public class CreateFiscalYearCommandHandler : IRequestHandler<CreateFiscalYearCommand, Int64>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public CreateFiscalYearCommandHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<Int64> Handle(CreateFiscalYearCommand request, CancellationToken cancellationToken)
    {
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = request.CompanyId,
            BranchId = request.BranchId,
            FiscalYearCode = request.FiscalYearCode,
            RowDesc = request.FiscalYearNameAr,
            RowDescE = request.FiscalYearNameEn,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsClosed = request.IsClosed,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _fiscalYearRepository.CreateAsync(fiscalYear);
    }
}
