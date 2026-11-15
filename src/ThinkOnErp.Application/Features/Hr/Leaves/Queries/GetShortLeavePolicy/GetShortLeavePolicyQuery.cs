using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeavePolicy;

public sealed record GetShortLeavePolicyQuery(long CompanyId) : IRequest<ShortLeavePolicyDto>;

public sealed class GetShortLeavePolicyQueryHandler : IRequestHandler<GetShortLeavePolicyQuery, ShortLeavePolicyDto>
{
    private readonly IShortLeaveRepository _repo;

    public GetShortLeavePolicyQueryHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShortLeavePolicyDto> Handle(GetShortLeavePolicyQuery request, CancellationToken ct)
    {
        var policy = await _repo.GetActivePolicyAsync(request.CompanyId, ct)
            ?? new ShortLeavePolicy { CompanyId = request.CompanyId };

        return ShortLeaveCommon.MapPolicyToDto(policy);
    }
}
