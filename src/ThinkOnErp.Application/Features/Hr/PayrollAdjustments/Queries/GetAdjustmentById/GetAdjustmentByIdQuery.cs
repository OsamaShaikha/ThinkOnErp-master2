using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustmentById;

public sealed record GetAdjustmentByIdQuery(
    long Id
) : IRequest<ApiResponse<PayrollAdjustment>>;

public sealed class GetAdjustmentByIdQueryHandler : IRequestHandler<GetAdjustmentByIdQuery, ApiResponse<PayrollAdjustment>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;

    public GetAdjustmentByIdQueryHandler(IPayrollAdjustmentRepository adjustmentRepository)
    {
        _adjustmentRepository = adjustmentRepository;
    }

    public async Task<ApiResponse<PayrollAdjustment>> Handle(GetAdjustmentByIdQuery request, CancellationToken cancellationToken)
    {
        var adjustment = await _adjustmentRepository.GetAdjustmentByIdAsync(request.Id, cancellationToken);
        if (adjustment == null)
            return ApiResponse<PayrollAdjustment>.CreateFailure("Adjustment not found.", statusCode: 404);

        return ApiResponse<PayrollAdjustment>.CreateSuccess(adjustment, "Adjustment retrieved successfully.");
    }
}
