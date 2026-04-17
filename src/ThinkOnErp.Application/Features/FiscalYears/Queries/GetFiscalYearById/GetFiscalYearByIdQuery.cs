using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearById;

public class GetFiscalYearByIdQuery : IRequest<FiscalYearDto?>
{
    public Int64 FiscalYearId { get; set; }
}
