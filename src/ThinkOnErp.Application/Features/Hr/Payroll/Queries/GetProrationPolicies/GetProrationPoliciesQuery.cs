using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetProrationPolicies;

public record GetProrationPoliciesQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<ProrationPolicy>>>;

public class GetProrationPoliciesQueryHandler : IRequestHandler<GetProrationPoliciesQuery, ApiResponse<IReadOnlyList<ProrationPolicy>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetProrationPoliciesQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<ProrationPolicy>>> Handle(GetProrationPoliciesQuery request, CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetProrationPoliciesAsync(request.CompanyId, cancellationToken);
        return ApiResponse<IReadOnlyList<ProrationPolicy>>.CreateSuccess(result, "Proration policies retrieved.");
    }
}
