using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetSSCPolicies;

public record GetSSCPoliciesQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<SSCPolicy>>>;

public class GetSSCPoliciesQueryHandler : IRequestHandler<GetSSCPoliciesQuery, ApiResponse<IReadOnlyList<SSCPolicy>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetSSCPoliciesQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<SSCPolicy>>> Handle(GetSSCPoliciesQuery request, CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetSSCPoliciesAsync(request.CompanyId, cancellationToken);
        return ApiResponse<IReadOnlyList<SSCPolicy>>.CreateSuccess(result, "SSC policies retrieved.");
    }
}
