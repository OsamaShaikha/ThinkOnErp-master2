using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServiceHandoffStatus;

public sealed record GetEndOfServiceHandoffStatusQuery(long SettlementId) : IRequest<ApiResponse<EndOfServiceHandoffStatusDto>>;

public sealed class GetEndOfServiceHandoffStatusQueryHandler : IRequestHandler<GetEndOfServiceHandoffStatusQuery, ApiResponse<EndOfServiceHandoffStatusDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;

    public GetEndOfServiceHandoffStatusQueryHandler(IEndOfServiceRepository eosRepo)
    {
        _eosRepo = eosRepo;
    }

    public async Task<ApiResponse<EndOfServiceHandoffStatusDto>> Handle(GetEndOfServiceHandoffStatusQuery request, CancellationToken ct)
    {
        var handoff = await _eosRepo.GetLatestHandoffAsync(request.SettlementId, ct);
        if (handoff == null)
            return ApiResponse<EndOfServiceHandoffStatusDto>.CreateFailure(
                "No accounting handoff found for this settlement.",
                statusCode: 404);

        return ApiResponse<EndOfServiceHandoffStatusDto>.CreateSuccess(
            new EndOfServiceHandoffStatusDto(
                handoff.Id,
                handoff.HandoffReference,
                handoff.DeliveryStatus,
                handoff.Operation,
                handoff.SourceVersion,
                handoff.CreatedAtUtc),
            "Handoff status retrieved successfully.");
    }
}
