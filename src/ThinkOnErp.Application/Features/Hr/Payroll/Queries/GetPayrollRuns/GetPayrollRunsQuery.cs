using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRuns;

public record GetPayrollRunsQuery(
    long CompanyId,
    string? Period,
    long? BranchId,
    string? Status,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResultDto<PayrollRunListItemDto>>>;

public class GetPayrollRunsQueryHandler : IRequestHandler<GetPayrollRunsQuery, ApiResponse<PagedResultDto<PayrollRunListItemDto>>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public GetPayrollRunsQueryHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<PagedResultDto<PayrollRunListItemDto>>> Handle(GetPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        if (request.PageIndex is < 1 or > 1_000_000 || request.PageSize is < 1 or > 100 || request.BranchId is <= 0)
            return ApiResponse<PagedResultDto<PayrollRunListItemDto>>.CreateFailure(
                "Page, page size or branch filter is invalid.", statusCode: 400);

        if (request.CompanyId <= 0) throw new ArgumentOutOfRangeException(nameof(request.CompanyId));

        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        var (runs, total) = await _payrollPeriodRepo.GetPayrollRunsAsync(
            request.CompanyId, string.IsNullOrWhiteSpace(request.Period) ? null : request.Period.Trim(), request.BranchId,
            normalizedStatus, request.PageIndex, request.PageSize, cancellationToken);

        var items = runs.Select(run => new PayrollRunListItemDto(
            run.Id, run.PayPeriod, run.BranchId, run.Status, run.RunDate, run.Lines.Count,
            run.TotalGrossSalary, run.TotalNetSalary, run.CorrectionVersion)).ToList();

        var result = new PagedResultDto<PayrollRunListItemDto>(items, total, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResultDto<PayrollRunListItemDto>>.CreateSuccess(result, "Payroll runs retrieved successfully.");
    }
}
