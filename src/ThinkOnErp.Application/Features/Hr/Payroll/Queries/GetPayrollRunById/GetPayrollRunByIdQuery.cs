using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRunById;

public record GetPayrollRunByIdQuery(long Id) : IRequest<ApiResponse<PayrollCalculationResultDto>>;

public class GetPayrollRunByIdQueryHandler : IRequestHandler<GetPayrollRunByIdQuery, ApiResponse<PayrollCalculationResultDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public GetPayrollRunByIdQueryHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<PayrollCalculationResultDto>> Handle(GetPayrollRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(request.Id, cancellationToken);
        if (run == null)
            return ApiResponse<PayrollCalculationResultDto>.CreateFailure("Payroll run not found.", statusCode: 404);

        var result = PayrollCommon.MapToResultDto(run);
        return ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run retrieved successfully.");
    }
}
