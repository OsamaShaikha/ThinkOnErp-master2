using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.ComparePayrollRun;

public record ComparePayrollRunQuery(
    long Id,
    long? BaselineRunId = null
) : IRequest<ApiResponse<PayrollRunComparisonDto>>;

public class ComparePayrollRunQueryHandler : IRequestHandler<ComparePayrollRunQuery, ApiResponse<PayrollRunComparisonDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public ComparePayrollRunQueryHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<PayrollRunComparisonDto>> Handle(ComparePayrollRunQuery request, CancellationToken cancellationToken)
    {
        var currentRun = await _payrollPeriodRepo.GetPayrollRunByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll run {request.Id} not found.");

        PayrollRun? baselineRun = null;
        if (request.BaselineRunId.HasValue)
        {
            baselineRun = await _payrollPeriodRepo.GetPayrollRunByIdAsync(request.BaselineRunId.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Baseline payroll run {request.BaselineRunId.Value} not found.");
        }
        else if (currentRun.CorrectionOfRunId.HasValue)
        {
            baselineRun = await _payrollPeriodRepo.GetPayrollRunByIdAsync(currentRun.CorrectionOfRunId.Value, cancellationToken);
        }
        else if (currentRun.BranchId.HasValue)
        {
            baselineRun = await _payrollPeriodRepo.GetPreviousPayrollRunAsync(currentRun.BranchId, currentRun.PayPeriod, currentRun.Id, cancellationToken);
        }

        var comparison = PayrollCommon.BuildComparison(currentRun, baselineRun);
        return ApiResponse<PayrollRunComparisonDto>.CreateSuccess(comparison, "Payroll comparison generated successfully.");
    }
}
