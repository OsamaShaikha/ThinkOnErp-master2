using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyEmployeeLink;

public sealed record GetMyEmployeeLinkQuery(long UserId) : IRequest<ApiResponse<EmployeeUserLinkDto>>;

public sealed class GetMyEmployeeLinkQueryHandler : IRequestHandler<GetMyEmployeeLinkQuery, ApiResponse<EmployeeUserLinkDto>>
{
    private readonly IEmployeeRepository _repository;

    public GetMyEmployeeLinkQueryHandler(IEmployeeRepository repository) => _repository = repository;

    public async Task<ApiResponse<EmployeeUserLinkDto>> Handle(GetMyEmployeeLinkQuery request, CancellationToken ct)
    {
        var employee = await _repository.GetEmployeeByUserIdAsync(request.UserId, ct);
        return employee == null
            ? ApiResponse<EmployeeUserLinkDto>.CreateFailure("No employee is linked to this account.", statusCode: 404)
            : ApiResponse<EmployeeUserLinkDto>.CreateSuccess(new EmployeeUserLinkDto(employee.EmployeeCode, employee.UserId), "Employee link retrieved successfully.");
    }
}
