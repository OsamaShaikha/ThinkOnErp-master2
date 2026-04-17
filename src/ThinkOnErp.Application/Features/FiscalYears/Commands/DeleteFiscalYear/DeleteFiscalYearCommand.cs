using MediatR;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.DeleteFiscalYear;

public class DeleteFiscalYearCommand : IRequest<Int64>
{
    public Int64 FiscalYearId { get; set; }
}
