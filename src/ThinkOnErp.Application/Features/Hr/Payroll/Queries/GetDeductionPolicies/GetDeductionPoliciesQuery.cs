using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetDeductionPolicies;

public record GetDeductionPoliciesQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<DeductionPolicy>>>;

public class GetDeductionPoliciesQueryHandler : IRequestHandler<GetDeductionPoliciesQuery, ApiResponse<IReadOnlyList<DeductionPolicy>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetDeductionPoliciesQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<DeductionPolicy>>> Handle(GetDeductionPoliciesQuery request, CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetDeductionPoliciesAsync(request.CompanyId, cancellationToken);
        return ApiResponse<IReadOnlyList<DeductionPolicy>>.CreateSuccess(result, "Deduction policies retrieved.");
    }
}
