using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.LinkEmployeeUser;

/// <summary>Links (UserId set) or unlinks (UserId null) the employee's login account.</summary>
public sealed record LinkEmployeeUserCommand(string EmployeeCode, long? UserId, string Username) : IRequest<ApiResponse<EmployeeUserLinkDto>>;

public sealed class LinkEmployeeUserCommandValidator : AbstractValidator<LinkEmployeeUserCommand>
{
    public LinkEmployeeUserCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserId).GreaterThan(0).When(x => x.UserId.HasValue).WithMessage("User ID must be positive.");
    }
}

public sealed class LinkEmployeeUserCommandHandler : IRequestHandler<LinkEmployeeUserCommand, ApiResponse<EmployeeUserLinkDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly IHrUnitOfWork _unitOfWork;

    public LinkEmployeeUserCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<EmployeeUserLinkDto>> Handle(LinkEmployeeUserCommand request, CancellationToken ct)
    {
        var code = EmployeeProfileRules.Required(request.EmployeeCode, "EmployeeCode", 100).ToUpperInvariant();
        var username = EmployeeProfileRules.Required(request.Username, "User", 100);
        if (request.UserId <= 0) throw new ArgumentException("User ID must be positive.");

        // All state checks run inside the account guard so they see the locked row.
        var link = await _unitOfWork.ExecuteEmployeeAccountAsync(code, () => LinkAsync(code, request.UserId, username, ct), ct);
        return ApiResponse<EmployeeUserLinkDto>.CreateSuccess(link, "Employee login account updated successfully.");
    }

    private async Task<EmployeeUserLinkDto> LinkAsync(string code, long? userId, string username, CancellationToken ct)
    {
        var employee = await _repository.GetEmployeeByCodeAsync(code, ct)
            ?? throw new KeyNotFoundException($"Employee with code '{code}' not found.");

        if (userId.HasValue)
            await EnsureUserCanBeLinkedAsync(employee, userId.Value, ct);

        if (employee.UserId == userId)
            return new EmployeeUserLinkDto(employee.EmployeeCode, employee.UserId);

        employee.UserId = userId;
        employee.UpdateUser = username;
        employee.UpdateDate = DateTime.UtcNow;
        await _repository.UpdateEmployeeUserLinkAsync(employee, ct);
        await _repository.SaveChangesAsync(ct);

        return new EmployeeUserLinkDto(employee.EmployeeCode, employee.UserId);
    }

    private async Task EnsureUserCanBeLinkedAsync(Employee employee, long userId, CancellationToken ct)
    {
        if (!employee.IsActive || employee.EmploymentStatus != "ACTIVE")
            throw new InvalidOperationException("Only active employees can be linked to a login account.");
        if (!await _repository.IsUserAvailableForEmployeeAsync(userId, employee.BranchId, ct))
            throw new InvalidOperationException("User must be active, exist in this tenant, and have access to the employee's branch.");

        var existing = await _repository.GetEmployeeByUserIdAsync(userId, ct);
        if (existing != null && existing.EmployeeCode != employee.EmployeeCode)
            throw new InvalidOperationException("This user is already linked to another employee.");
    }
}
