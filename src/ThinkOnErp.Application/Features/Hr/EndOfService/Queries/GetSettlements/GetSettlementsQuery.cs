using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlements;

public sealed record GetSettlementsQuery(
    long CompanyId,
    long? BranchId = null,
    string? Status = null
) : IRequest<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>;

public sealed class GetSettlementsQueryHandler : IRequestHandler<GetSettlementsQuery, ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>
{
    private readonly IEndOfServiceRepository _eosRepo;

    public GetSettlementsQueryHandler(IEndOfServiceRepository eosRepo)
    {
        _eosRepo = eosRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>> Handle(GetSettlementsQuery request, CancellationToken ct)
    {
        var list = await _eosRepo.GetListAsync(request.CompanyId, request.BranchId, request.Status, ct);
        return ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>.CreateSuccess(
            list.Select(EndOfServiceMappings.ToDto).ToList(),
            "End of service settlements retrieved successfully.");
    }
}
