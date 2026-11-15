using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetActiveSalaryStructure;

/// <param name="AsOfDate">Defaults to now (UTC) when null.</param>
public sealed record GetActiveSalaryStructureQuery(string EmployeeCode, DateTime? AsOfDate) : IRequest<ApiResponse<SalaryStructureDetailsDto>>;

public sealed class GetActiveSalaryStructureQueryHandler : IRequestHandler<GetActiveSalaryStructureQuery, ApiResponse<SalaryStructureDetailsDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeFieldGuard _fields;

    public GetActiveSalaryStructureQueryHandler(IEmployeeRepository repository, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _fields = fields;
    }

    public async Task<ApiResponse<SalaryStructureDetailsDto>> Handle(GetActiveSalaryStructureQuery request, CancellationToken ct)
    {
        if (_fields.IsEnforced)
        {
            var employee = await _repository.GetEmployeeByCodeAsync(request.EmployeeCode, ct) ?? throw new KeyNotFoundException("Employee not found.");
            _fields.Require(EmployeeFieldAccess.ViewSalary, employee.BranchId);
        }

        var structure = await _repository.GetActiveSalaryStructureAsync(request.EmployeeCode, request.AsOfDate ?? DateTime.UtcNow, ct);
        return structure == null
            ? ApiResponse<SalaryStructureDetailsDto>.CreateFailure($"No active salary structure found for employee '{request.EmployeeCode}'.", statusCode: 404)
            : ApiResponse<SalaryStructureDetailsDto>.CreateSuccess(EmployeeMappings.ToStructureDetails(structure), "Salary structure retrieved successfully.");
    }
}
