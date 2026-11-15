using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServicePolicyById;

public sealed record GetEndOfServicePolicyByIdQuery(long Id, long CompanyId) : IRequest<ApiResponse<EndOfServicePolicy>>;

public sealed class GetEndOfServicePolicyByIdQueryHandler : IRequestHandler<GetEndOfServicePolicyByIdQuery, ApiResponse<EndOfServicePolicy>>
{
    private readonly IEndOfServiceRepository _policies;

    public GetEndOfServicePolicyByIdQueryHandler(IEndOfServiceRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<EndOfServicePolicy>> Handle(GetEndOfServicePolicyByIdQuery request, CancellationToken ct)
    {
        var result = (await _policies.GetPoliciesAsync(request.CompanyId, ct)).SingleOrDefault(p => p.Id == request.Id);
        if (result == null)
            return ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy not found.", statusCode: 404);

        return ApiResponse<EndOfServicePolicy>.CreateSuccess(result);
    }
}
