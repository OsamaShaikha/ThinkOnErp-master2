using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployeeByCode;

public sealed record GetEmployeeByCodeQuery(string EmployeeCode) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class GetEmployeeByCodeQueryHandler : IRequestHandler<GetEmployeeByCodeQuery, ApiResponse<EmployeeDetailsDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeFieldGuard _fields;

    public GetEmployeeByCodeQueryHandler(IEmployeeRepository repository, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _fields = fields;
    }

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(GetEmployeeByCodeQuery request, CancellationToken ct)
    {
        var employee = await _repository.GetEmployeeByCodeAsync(request.EmployeeCode, ct);
        if (employee == null)
            return ApiResponse<EmployeeDetailsDto>.CreateFailure($"Employee with code '{request.EmployeeCode}' not found.", statusCode: 404);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(_fields.Protect(EmployeeMappings.ToDetails(employee)), "Employee retrieved successfully.");
    }
}
