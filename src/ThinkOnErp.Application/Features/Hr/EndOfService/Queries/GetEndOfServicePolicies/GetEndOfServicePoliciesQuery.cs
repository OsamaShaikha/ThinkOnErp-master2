using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServicePolicies;

public sealed record GetEndOfServicePoliciesQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<EndOfServicePolicy>>>;

public sealed class GetEndOfServicePoliciesQueryHandler : IRequestHandler<GetEndOfServicePoliciesQuery, ApiResponse<IReadOnlyList<EndOfServicePolicy>>>
{
    private readonly IEndOfServiceRepository _policies;

    public GetEndOfServicePoliciesQueryHandler(IEndOfServiceRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<IReadOnlyList<EndOfServicePolicy>>> Handle(GetEndOfServicePoliciesQuery request, CancellationToken ct)
    {
        var result = await _policies.GetPoliciesAsync(request.CompanyId, ct);
        return ApiResponse<IReadOnlyList<EndOfServicePolicy>>.CreateSuccess(result);
    }
}
