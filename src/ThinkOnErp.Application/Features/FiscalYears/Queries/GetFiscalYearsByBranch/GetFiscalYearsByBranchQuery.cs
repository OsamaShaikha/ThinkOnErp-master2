using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByBranch;

public class GetFiscalYearsByBranchQuery : IRequest<List<FiscalYearDto>>
{
    public Int64 BranchId { get; set; }
}
