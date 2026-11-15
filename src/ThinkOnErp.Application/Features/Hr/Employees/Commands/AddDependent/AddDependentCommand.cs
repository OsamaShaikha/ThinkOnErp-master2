using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.AddDependent;

public sealed record AddDependentCommand(string EmployeeCode, CreateDependentDto Dto, string Username) : IRequest<ApiResponse<DependentDto>>;

public sealed class AddDependentCommandValidator : AbstractValidator<AddDependentCommand>
{
    public AddDependentCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Dependent data is required.");
    }
}

public sealed class AddDependentCommandHandler : IRequestHandler<AddDependentCommand, ApiResponse<DependentDto>>
{
    private readonly IEmployeeRepository _repository;
    private readonly EmployeeFieldGuard _fields;
    private readonly EmployeeProfileWriter _writer;

    public AddDependentCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _fields = fields;
        _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);
    }

    public async Task<ApiResponse<DependentDto>> Handle(AddDependentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Dto);
        // Dependents feed tax exemptions, so they are payroll inputs and need the profile guard.
        var dependent = await _writer.ProtectAsync(request.EmployeeCode, null, () => AddAsync(request, ct), ct);
        return ApiResponse<DependentDto>.CreateSuccess(dependent, "Dependent added successfully.");
    }

    private async Task<DependentDto> AddAsync(AddDependentCommand request, CancellationToken ct)
    {
        var employee = await _repository.GetEmployeeByCodeAsync(request.EmployeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee with code '{request.EmployeeCode}' not found.");
        _fields.Require(EmployeeFieldAccess.EditIdentity, employee.BranchId);
        _fields.Require(EmployeeFieldAccess.ViewIdentity, employee.BranchId);

        var dto = request.Dto;
        var dependent = new EmployeeDependent
        {
            EmployeeCode = request.EmployeeCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            Relationship = dto.Relationship,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            NationalId = dto.NationalId,
            IsTaxExemptionClaimed = dto.IsTaxExemptionClaimed,
            IsMedicalCovered = dto.IsMedicalCovered,
            IsActive = true,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };
        EmployeeDependentRules.NormalizeAndValidate(dependent);

        // TaxExemptionCount is for additional manual exemptions; dependents are counted separately.
        await _repository.AddDependentAsync(dependent, ct);
        await _repository.SaveChangesAsync(ct);

        return EmployeeMappings.ToDependent(dependent);
    }
}
