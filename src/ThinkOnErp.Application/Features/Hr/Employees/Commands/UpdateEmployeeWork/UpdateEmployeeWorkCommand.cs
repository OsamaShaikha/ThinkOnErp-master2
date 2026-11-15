using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeWork;

/// <summary>Updates the work profile only (names, dates, department, position, branch, manager, status).</summary>
public sealed record UpdateEmployeeWorkCommand(string EmployeeCode, UpdateEmployeeWorkDto Dto, string Username) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class UpdateEmployeeWorkCommandValidator : AbstractValidator<UpdateEmployeeWorkCommand>
{
    public UpdateEmployeeWorkCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Work profile data is required.");
    }
}

public sealed class UpdateEmployeeWorkCommandHandler : IRequestHandler<UpdateEmployeeWorkCommand, ApiResponse<EmployeeDetailsDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeProfileWriter _writer;

    public UpdateEmployeeWorkCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);
    }

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(UpdateEmployeeWorkCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);
        var code = EmployeeProfileRules.Required(request.EmployeeCode, "employeeCode", 100).ToUpperInvariant();

        var updated = await _writer.ProtectAsync(code, dto.HireDate.Date, async () =>
        {
            // Work fields come from the request; identity and bank fields from the locked row.
            var e = await _repository.GetEmployeeByCodeAsync(code, ct) ?? throw new KeyNotFoundException("Employee not found.");
            var replacement = new UpdateEmployeeDto(
                dto.NameLocal, dto.NameEn, e.NationalId, e.Nationality, e.PassportNumber, e.DateOfBirth, e.Gender, e.MaritalStatus, e.Email, e.Phone,
                dto.HireDate, dto.ProbationEndDate, dto.TerminationDate, dto.TerminationReason, dto.EmploymentType, dto.EmploymentStatus,
                dto.DepartmentCode, dto.PositionCode, dto.BranchId, dto.ManagerEmployeeCode, e.SscNumber, e.TaxExemptionCount, e.IsHighRiskRole,
                e.BankName, e.BankAccountNumber, e.BankIban, dto.IsActive);
            replacement = EmployeeInputValidation.Normalize(code, replacement);
            return await _writer.ApplyProfileAsync(code, replacement, request.Username, limitedUpdate: true, ct);
        }, ct);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(updated);
    }
}
