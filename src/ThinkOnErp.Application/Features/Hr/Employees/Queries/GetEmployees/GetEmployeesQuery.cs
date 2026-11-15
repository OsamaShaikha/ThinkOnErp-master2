using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployees;

public sealed record GetEmployeesQuery(
    string? SearchKeyword,
    string? DepartmentCode,
    string? Status,
    long? BranchId,
    int PageIndex = 1,
    int PageSize = 20) : IRequest<ApiResponse<PagedResultDto<EmployeeSummaryDto>>>;

public sealed class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, ApiResponse<PagedResultDto<EmployeeSummaryDto>>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeFieldGuard _fields;

    public GetEmployeesQueryHandler(IEmployeeRepository repository, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _fields = fields;
    }

    public async Task<ApiResponse<PagedResultDto<EmployeeSummaryDto>>> Handle(GetEmployeesQuery request, CancellationToken ct)
    {
        var (employees, totalCount) = await _repository.GetEmployeesAsync(
            request.SearchKeyword, request.DepartmentCode, request.Status, request.BranchId, request.PageIndex, request.PageSize, ct);

        var today = DateTime.UtcNow.Date;
        var items = employees
            .Select(e => _fields.Protect(EmployeeMappings.ToSummary(e, today), e.BranchId))
            .ToList();

        var page = new PagedResultDto<EmployeeSummaryDto>(items, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResultDto<EmployeeSummaryDto>>.CreateSuccess(page, "Employees retrieved successfully.");
    }
}
