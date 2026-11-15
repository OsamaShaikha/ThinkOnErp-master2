using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetCurrencyPrecision;

public record GetCurrencyPrecisionQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>>;

public class GetCurrencyPrecisionQueryHandler : IRequestHandler<GetCurrencyPrecisionQuery, ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetCurrencyPrecisionQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>> Handle(GetCurrencyPrecisionQuery request, CancellationToken cancellationToken)
    {
        if (request.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");

        var settings = await _policyRepo.GetPayrollCurrencyPrecisionsAsync(request.CompanyId, cancellationToken);
        var result = settings.Select(x => new PayrollCurrencyPrecisionDto
        {
            CompanyId = x.CompanyId,
            CurrencyCode = x.CurrencyCode,
            DecimalPlaces = x.DecimalPlaces
        }).ToArray();

        return ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>.CreateSuccess(result, "Payroll currency precision retrieved.");
    }
}
