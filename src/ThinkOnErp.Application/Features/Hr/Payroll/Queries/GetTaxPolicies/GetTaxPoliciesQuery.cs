using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetTaxPolicies;

public record GetTaxPoliciesQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<TaxPolicy>>>;

public class GetTaxPoliciesQueryHandler : IRequestHandler<GetTaxPoliciesQuery, ApiResponse<IReadOnlyList<TaxPolicy>>>
{
    private readonly IPolicyRepository _policyRepo;

    public GetTaxPoliciesQueryHandler(IPolicyRepository policyRepo)
    {
        _policyRepo = policyRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<TaxPolicy>>> Handle(GetTaxPoliciesQuery request, CancellationToken cancellationToken)
    {
        var result = await _policyRepo.GetTaxPoliciesAsync(request.CompanyId, cancellationToken);
        return ApiResponse<IReadOnlyList<TaxPolicy>>.CreateSuccess(result, "Tax policies retrieved.");
    }
}
