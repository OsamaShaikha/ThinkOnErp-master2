using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(CreateEmployeeDto Dto, string Username) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Employee data is required.");
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, ApiResponse<EmployeeDetailsDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly IHrUnitOfWork _unitOfWork;
    private readonly EmployeeFieldGuard _fields;

    public CreateEmployeeCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fields = fields;
    }

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Dto);
        var dto = EmployeeInputValidation.Normalize(request.Dto);
        RequireCreatePermissions(dto);

        // Lock the new code + hire date so payroll cannot be approved for this employee mid-create.
        var lockRow = new EmployeeImportRowDto { EmployeeCode = dto.EmployeeCode, BranchId = dto.BranchId, HireDate = dto.HireDate.Date };
        var created = await _unitOfWork.ExecuteNewEmployeeAsync(lockRow, () => CreateAsync(dto, request.Username, ct), ct);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(created, "Employee created successfully.");
    }

    private void RequireCreatePermissions(CreateEmployeeDto dto)
    {
        _fields.Require(EmployeeFieldAccess.EditIdentity, dto.BranchId);
        if (dto.BankName != null || dto.BankAccountNumber != null || dto.BankIban != null)
            _fields.Require(EmployeeFieldAccess.EditBank, dto.BranchId);
    }

    private async Task<EmployeeDetailsDto> CreateAsync(CreateEmployeeDto dto, string username, CancellationToken ct)
    {
        if (await _repository.ExistsByCodeAsync(dto.EmployeeCode, ct))
            throw new InvalidOperationException($"Employee with code '{dto.EmployeeCode}' already exists.");

        if (await _repository.ExistsByNationalIdAsync(dto.NationalId, null, ct))
            throw new InvalidOperationException("National ID is already assigned to another employee.");

        var employee = new Employee
        {
            EmployeeCode = dto.EmployeeCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            NationalId = dto.NationalId,
            Nationality = dto.Nationality,
            CitizenshipCountryCode = string.IsNullOrEmpty(dto.CitizenshipCountryCode) ? null : dto.CitizenshipCountryCode,
            PassportNumber = dto.PassportNumber,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            Email = dto.Email,
            Phone = dto.Phone,
            HireDate = dto.HireDate,
            ProbationEndDate = dto.ProbationEndDate,
            TerminationDate = dto.TerminationDate,
            TerminationReason = dto.TerminationReason,
            EmploymentType = dto.EmploymentType,
            EmploymentStatus = dto.EmploymentStatus,
            DepartmentCode = dto.DepartmentCode,
            PositionCode = dto.PositionCode,
            BranchId = dto.BranchId,
            ManagerEmployeeCode = dto.ManagerEmployeeCode,
            SscNumber = dto.SscNumber,
            TaxExemptionCount = dto.TaxExemptionCount,
            IsHighRiskRole = dto.IsHighRiskRole,
            BankName = dto.BankName,
            BankAccountNumber = dto.BankAccountNumber,
            BankIban = dto.BankIban,
            IsActive = dto.EmploymentStatus != "TERMINATED",
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddEmployeeAsync(employee, ct);
        await _repository.SaveChangesAsync(ct);

        return _fields.Protect(EmployeeMappings.ToDetails(employee));
    }
}
