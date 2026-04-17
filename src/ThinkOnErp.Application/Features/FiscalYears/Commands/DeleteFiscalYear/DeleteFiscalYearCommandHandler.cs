using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.DeleteFiscalYear;

public class DeleteFiscalYearCommandHandler : IRequestHandler<DeleteFiscalYearCommand, Int64>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public DeleteFiscalYearCommandHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<Int64> Handle(DeleteFiscalYearCommand request, CancellationToken cancellationToken)
    {
        return await _fiscalYearRepository.DeleteAsync(request.FiscalYearId);
    }
}
