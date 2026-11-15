using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetMyPayslips;

public record GetMyPayslipsQuery(
    long CompanyId,
    long UserId,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResultDto<PayslipSummaryDto>>>;

public class GetMyPayslipsQueryHandler : IRequestHandler<GetMyPayslipsQuery, ApiResponse<PagedResultDto<PayslipSummaryDto>>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IEmployeeRepository? _employeeRepository;

    public GetMyPayslipsQueryHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IEmployeeRepository? employeeRepository = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _employeeRepository = employeeRepository;
    }

    public async Task<ApiResponse<PagedResultDto<PayslipSummaryDto>>> Handle(GetMyPayslipsQuery request, CancellationToken cancellationToken)
    {
        if (request.PageIndex is < 1 or > 1_000_000 || request.PageSize is < 1 or > 100)
            return ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateFailure("Page or page size is invalid.", statusCode: 400);

        if (request.CompanyId <= 0 || request.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.UserId));

        if (_employeeRepository == null)
            throw new InvalidOperationException("Employee repository is required for self-service payslips.");

        var employee = await _employeeRepository.GetEmployeeByUserIdAsync(request.UserId, cancellationToken);
        if (employee == null)
            return ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateFailure("No employee is linked to this account.", statusCode: 404);

        var (lines, total) = await _payrollPeriodRepo.GetEmployeePayslipsAsync(
            request.CompanyId, employee.EmployeeCode, request.PageIndex, request.PageSize, cancellationToken);

        var result = new PagedResultDto<PayslipSummaryDto>(lines.Select(PayrollCommon.MapPayslipSummary).ToList(), total, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateSuccess(result, "Payslips retrieved successfully.");
    }
}
