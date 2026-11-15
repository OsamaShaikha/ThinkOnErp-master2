using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetOvertimeRules;

public record GetOvertimeRulesQuery(
    long CompanyId,
    DateTime? EffectiveDate = null
) : IRequest<ApiResponse<IReadOnlyList<OvertimeRule>>>;

public class GetOvertimeRulesQueryHandler : IRequestHandler<GetOvertimeRulesQuery, ApiResponse<IReadOnlyList<OvertimeRule>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetOvertimeRulesQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<OvertimeRule>>> Handle(GetOvertimeRulesQuery request, CancellationToken cancellationToken)
    {
        if (request.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");

        var result = await _policyRepo.GetActiveOvertimeRulesAsync(
            request.CompanyId, request.EffectiveDate?.Date ?? DateTime.UtcNow.Date, cancellationToken);

        return ApiResponse<IReadOnlyList<OvertimeRule>>.CreateSuccess(result, "Overtime rules retrieved.");
    }
}
