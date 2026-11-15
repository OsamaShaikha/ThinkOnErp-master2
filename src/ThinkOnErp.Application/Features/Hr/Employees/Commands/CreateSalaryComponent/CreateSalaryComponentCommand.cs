using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.CreateSalaryComponent;

public sealed record CreateSalaryComponentCommand(CreateSalaryComponentDto Dto, string Username) : IRequest<ApiResponse<SalaryComponentDto>>;

public sealed class CreateSalaryComponentCommandValidator : AbstractValidator<CreateSalaryComponentCommand>
{
    public CreateSalaryComponentCommandValidator()
        => RuleFor(x => x.Dto).NotNull().WithMessage("Salary component data is required.");
}

public sealed class CreateSalaryComponentCommandHandler : IRequestHandler<CreateSalaryComponentCommand, ApiResponse<SalaryComponentDto>>
{
    private readonly IEmployeeRepository _repository;

    public CreateSalaryComponentCommandHandler(IEmployeeRepository repository) => _repository = repository;

    public async Task<ApiResponse<SalaryComponentDto>> Handle(CreateSalaryComponentCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);

        var component = new SalaryComponent
        {
            ComponentCode = dto.ComponentCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            ComponentType = dto.ComponentType,
            CalculationType = dto.CalculationType,
            DefaultAmount = dto.DefaultAmount,
            DefaultPercent = dto.DefaultPercent,
            IsTaxable = dto.IsTaxable,
            IsSscApplicable = dto.IsSscApplicable,
            GlAccountCode = dto.GlAccountCode,
            IsActive = true,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };
        SalaryComponentRules.NormalizeAndValidate(component);

        if (await _repository.GetSalaryComponentByCodeAsync(component.ComponentCode, ct) != null)
            throw new InvalidOperationException($"Salary component '{component.ComponentCode}' already exists.");

        await _repository.AddSalaryComponentAsync(component, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<SalaryComponentDto>.CreateSuccess(EmployeeMappings.ToSalaryComponent(component), "Salary component created successfully.");
    }
}
