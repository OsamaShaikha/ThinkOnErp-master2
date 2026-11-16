using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPeriods;

public record GetPeriodsQuery(
    long CompanyId,
    int? Year = null
) : IRequest<ApiResponse<IReadOnlyList<PayrollPeriod>>>;

public class GetPeriodsQueryHandler : IRequestHandler<GetPeriodsQuery, ApiResponse<IReadOnlyList<PayrollPeriod>>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public GetPeriodsQueryHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<PayrollPeriod>>> Handle(GetPeriodsQuery request, CancellationToken cancellationToken)
    {
        var periods = await _payrollPeriodRepo.GetPeriodsAsync(request.CompanyId, request.Year, cancellationToken);
        return ApiResponse<IReadOnlyList<PayrollPeriod>>.CreateSuccess(periods, "Payroll periods retrieved successfully.");
    }
}
