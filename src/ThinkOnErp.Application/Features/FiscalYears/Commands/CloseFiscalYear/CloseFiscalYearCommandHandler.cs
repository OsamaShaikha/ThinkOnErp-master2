using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.CloseFiscalYear;

public class CloseFiscalYearCommandHandler : IRequestHandler<CloseFiscalYearCommand, Int64>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public CloseFiscalYearCommandHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<Int64> Handle(CloseFiscalYearCommand request, CancellationToken cancellationToken)
    {
        return await _fiscalYearRepository.CloseAsync(request.FiscalYearId, request.UpdateUser);
    }
}
