using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployee;

/// <summary>Full profile replacement (all field groups).</summary>
public sealed record UpdateEmployeeCommand(string EmployeeCode, UpdateEmployeeDto Dto, string Username) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Employee data is required.");
    }
}

public sealed class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, ApiResponse<EmployeeDetailsDto>>
{
    private readonly EmployeeProfileWriter _writer;
    private readonly EmployeeFieldGuard _fields;

    public UpdateEmployeeCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);
        _fields = fields;
    }

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Dto);
        var code = EmployeeProfileRules.Required(request.EmployeeCode, "employeeCode", 100).ToUpperInvariant();
        var dto = EmployeeInputValidation.Normalize(code, request.Dto);

        _fields.Require(EmployeeFieldAccess.EditIdentity, dto.BranchId);
        _fields.Require(EmployeeFieldAccess.EditBank, dto.BranchId);

        var updated = await _writer.ProtectAsync(code, dto.HireDate.Date,
            () => _writer.ApplyProfileAsync(code, dto, request.Username, limitedUpdate: false, ct), ct);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(updated, "Employee updated successfully.");
    }
}
