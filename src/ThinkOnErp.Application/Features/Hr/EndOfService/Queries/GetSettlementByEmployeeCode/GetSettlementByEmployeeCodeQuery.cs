using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementByEmployeeCode;

public sealed record GetSettlementByEmployeeCodeQuery(string EmployeeCode) : IRequest<ApiResponse<EndOfServiceSettlementDto>>;

public sealed class GetSettlementByEmployeeCodeQueryHandler : IRequestHandler<GetSettlementByEmployeeCodeQuery, ApiResponse<EndOfServiceSettlementDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;

    public GetSettlementByEmployeeCodeQueryHandler(IEndOfServiceRepository eosRepo)
    {
        _eosRepo = eosRepo;
    }

    public async Task<ApiResponse<EndOfServiceSettlementDto>> Handle(GetSettlementByEmployeeCodeQuery request, CancellationToken ct)
    {
        var settlement = await _eosRepo.GetByEmployeeCodeAsync(request.EmployeeCode, ct);
        if (settlement == null)
            return ApiResponse<EndOfServiceSettlementDto>.CreateFailure(
                $"No active end of service settlement found for employee '{request.EmployeeCode}'.",
                statusCode: 404);

        return ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(
            EndOfServiceMappings.ToDto(settlement),
            "End of service settlement retrieved successfully.");
    }
}
