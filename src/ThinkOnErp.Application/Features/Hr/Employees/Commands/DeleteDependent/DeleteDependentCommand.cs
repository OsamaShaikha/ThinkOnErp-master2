using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.DeleteDependent;

public sealed record DeleteDependentCommand(long DependentId, string Username) : IRequest<ApiResponse<bool>>;

public sealed class DeleteDependentCommandValidator : AbstractValidator<DeleteDependentCommand>
{
    public DeleteDependentCommandValidator() => RuleFor(x => x.DependentId).GreaterThan(0);
}

public sealed class DeleteDependentCommandHandler : IRequestHandler<DeleteDependentCommand, ApiResponse<bool>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeFieldGuard _fields;
    private readonly EmployeeProfileWriter _writer;

    public DeleteDependentCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _fields = fields;
        _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);
    }

    public async Task<ApiResponse<bool>> Handle(DeleteDependentCommand request, CancellationToken ct)
    {
        var snapshot = await _repository.GetDependentByIdAsync(request.DependentId, ct)
            ?? throw new KeyNotFoundException("Dependent not found in current HR scope.");
        var employeeCode = snapshot.EmployeeCode;

        await _writer.ProtectAsync(employeeCode, null, async () =>
        {
            // Re-read inside the lock: the dependent may have been moved or removed meanwhile.
            var current = await _repository.GetDependentByIdAsync(request.DependentId, ct)
                ?? throw new KeyNotFoundException("Dependent no longer exists in current HR scope.");
            if (current.EmployeeCode != employeeCode)
                throw new InvalidOperationException("Dependent ownership changed. Reload and retry.");

            var parent = await _repository.GetEmployeeByCodeAsync(employeeCode, ct)
                ?? throw new KeyNotFoundException("Employee not found.");
            _fields.Require(EmployeeFieldAccess.EditIdentity, parent.BranchId);

            await _repository.DeleteDependentAsync(current, ct);
            await _repository.SaveChangesAsync(ct);
            return true;
        }, ct);

        return ApiResponse<bool>.CreateSuccess(true, "Dependent deleted successfully.");
    }
}
