using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetSalaryComponents;

public sealed record GetSalaryComponentsQuery : IRequest<ApiResponse<IReadOnlyList<SalaryComponentDto>>>;

public sealed class GetSalaryComponentsQueryHandler : IRequestHandler<GetSalaryComponentsQuery, ApiResponse<IReadOnlyList<SalaryComponentDto>>>
{
    private readonly IEmployeeRepository _repository;

    public GetSalaryComponentsQueryHandler(IEmployeeRepository repository) => _repository = repository;

    public async Task<ApiResponse<IReadOnlyList<SalaryComponentDto>>> Handle(GetSalaryComponentsQuery request, CancellationToken ct)
    {
        var components = await _repository.GetSalaryComponentsAsync(ct);
        IReadOnlyList<SalaryComponentDto> items = components.Select(EmployeeMappings.ToSalaryComponent).ToList();
        return ApiResponse<IReadOnlyList<SalaryComponentDto>>.CreateSuccess(items, "Salary components retrieved successfully.");
    }
}
