using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Queries.GetAdjustments;

public sealed record GetAdjustmentsQuery(
    long CompanyId,
    string? EmployeeCode = null,
    string? PayPeriod = null,
    string? Status = null
) : IRequest<ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>>;

public sealed class GetAdjustmentsQueryHandler : IRequestHandler<GetAdjustmentsQuery, ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;

    public GetAdjustmentsQueryHandler(IPayrollAdjustmentRepository adjustmentRepository)
    {
        _adjustmentRepository = adjustmentRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>> Handle(GetAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var list = await _adjustmentRepository.GetAdjustmentsAsync(
            request.CompanyId,
            request.EmployeeCode,
            request.PayPeriod,
            request.Status,
            cancellationToken);

        var dtos = (IReadOnlyList<PayrollAdjustmentDto>)list.Select(PayrollAdjustmentCommon.ToDto).ToList();
        return ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>.CreateSuccess(dtos, "Adjustments retrieved successfully.");
    }
}
