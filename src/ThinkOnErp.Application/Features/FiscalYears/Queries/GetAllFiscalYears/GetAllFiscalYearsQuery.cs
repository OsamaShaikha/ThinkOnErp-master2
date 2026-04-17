using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetAllFiscalYears;

public class GetAllFiscalYearsQuery : IRequest<List<FiscalYearDto>>
{
}
