using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementById;

public sealed record GetSettlementByIdQuery(long SettlementId) : IRequest<ApiResponse<EndOfServiceSettlementDto>>;

public sealed class GetSettlementByIdQueryHandler : IRequestHandler<GetSettlementByIdQuery, ApiResponse<EndOfServiceSettlementDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;

    public GetSettlementByIdQueryHandler(IEndOfServiceRepository eosRepo)
    {
        _eosRepo = eosRepo;
    }

    public async Task<ApiResponse<EndOfServiceSettlementDto>> Handle(GetSettlementByIdQuery request, CancellationToken ct)
    {
        var settlement = await _eosRepo.GetByIdAsync(request.SettlementId, ct);
        if (settlement == null)
            return ApiResponse<EndOfServiceSettlementDto>.CreateFailure("End of service settlement not found.", statusCode: 404);

        return ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(
            EndOfServiceMappings.ToDto(settlement),
            "End of service settlement retrieved successfully.");
    }
}
