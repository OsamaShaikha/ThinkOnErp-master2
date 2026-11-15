using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteEmployee;

/// <summary>
/// Employees are never deleted or terminated here: termination belongs to the End of Service workflow.
/// The command still runs under the payroll guard so callers get the same not-found / locked-payroll
/// answers as every other profile mutation.
/// </summary>
public sealed record DeleteEmployeeCommand(string EmployeeCode, string Username) : IRequest<ApiResponse<bool>>;

public sealed class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand, ApiResponse<bool>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeProfileWriter _writer;

    public DeleteEmployeeCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);
    }

    public async Task<ApiResponse<bool>> Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        await _writer.ProtectAsync<bool>(request.EmployeeCode, null, async () =>
        {
            _ = await _repository.GetEmployeeByCodeAsync(request.EmployeeCode, ct)
                ?? throw new KeyNotFoundException($"Employee with code '{request.EmployeeCode}' not found.");
            throw new InvalidOperationException(
                "Employees cannot be terminated or deleted through this endpoint. Complete termination through End of Service.");
        }, ct);

        return ApiResponse<bool>.CreateSuccess(true, "Employee deactivated successfully.");
    }
}
