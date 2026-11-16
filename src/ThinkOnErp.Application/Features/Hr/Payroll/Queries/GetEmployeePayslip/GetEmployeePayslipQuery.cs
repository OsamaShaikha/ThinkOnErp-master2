using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetEmployeePayslip;

public record GetEmployeePayslipQuery(
    long CompanyId,
    long PayrollRunId,
    string EmployeeCode
) : IRequest<ApiResponse<PayslipSummaryDto>>;

public class GetEmployeePayslipQueryHandler : IRequestHandler<GetEmployeePayslipQuery, ApiResponse<PayslipSummaryDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public GetEmployeePayslipQueryHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<PayslipSummaryDto>> Handle(GetEmployeePayslipQuery request, CancellationToken cancellationToken)
    {
        if (request.CompanyId <= 0 || request.PayrollRunId <= 0 || string.IsNullOrWhiteSpace(request.EmployeeCode))
            return ApiResponse<PayslipSummaryDto>.CreateFailure("Approved payslip not found.", statusCode: 404);

        var line = await _payrollPeriodRepo.GetEmployeePayslipAsync(
            request.CompanyId, request.PayrollRunId, request.EmployeeCode.Trim(), cancellationToken);

        if (line == null)
            return ApiResponse<PayslipSummaryDto>.CreateFailure("Approved payslip not found.", statusCode: 404);

        var result = PayrollCommon.MapPayslipSummary(line);
        return ApiResponse<PayslipSummaryDto>.CreateSuccess(result, "Payslip retrieved successfully.");
    }
}
