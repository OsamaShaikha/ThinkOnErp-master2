using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByCompany;

public class GetFiscalYearsByCompanyQuery : IRequest<List<FiscalYearDto>>
{
    public Int64 CompanyId { get; set; }
}
