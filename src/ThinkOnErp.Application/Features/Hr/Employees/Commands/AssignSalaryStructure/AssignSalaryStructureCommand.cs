using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.AssignSalaryStructure;

/// <summary>
/// Assigns a new salary structure from <c>EffectiveFrom</c>, truncating the structure that was active on that day.
/// </summary>
public sealed record AssignSalaryStructureCommand(string EmployeeCode, AssignSalaryStructureDto Dto, string Username) : IRequest<ApiResponse<SalaryStructureDetailsDto>>;

public sealed class AssignSalaryStructureCommandValidator : AbstractValidator<AssignSalaryStructureCommand>
{
    public AssignSalaryStructureCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Salary structure data is required.");
    }
}

public sealed class AssignSalaryStructureCommandHandler : IRequestHandler<AssignSalaryStructureCommand, ApiResponse<SalaryStructureDetailsDto>>
{
    private const string InvalidRangeMessage = "Salary amount and effective date range are invalid.";

    private readonly IEmployeeRepository _repository;
    private readonly IHrUnitOfWork _unitOfWork;
    private readonly EmployeeFieldGuard _fields;

    public AssignSalaryStructureCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fields = fields;
    }

    public async Task<ApiResponse<SalaryStructureDetailsDto>> Handle(AssignSalaryStructureCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Dto);
        var code = request.EmployeeCode;

        // Salary structures are effective by calendar day throughout HR and payroll.
        var dto = request.Dto with { EffectiveFrom = request.Dto.EffectiveFrom.Date, EffectiveTo = request.Dto.EffectiveTo?.Date };

        if (_fields.IsEnforced)
        {
            var employee = await _repository.GetEmployeeByCodeAsync(code, ct) ?? throw new KeyNotFoundException("Employee not found.");
            RequireSalaryPermissions(employee.BranchId);
        }
        EnsureValidAmountAndRange(dto);

        // Truncating the preceding structure can affect every later period, even when the
        // new structure has an end date, so the lock runs from EffectiveFrom to the end of time.
        var structure = await _unitOfWork.ExecuteForEmployeeDatesAsync(code, dto.EffectiveFrom, DateTime.MaxValue.Date,
            () => AssignAsync(code, dto, request.Username, ct), ct);

        return ApiResponse<SalaryStructureDetailsDto>.CreateSuccess(structure, "Salary structure assigned successfully.");
    }

    private async Task<SalaryStructureDetailsDto> AssignAsync(string code, AssignSalaryStructureDto dto, string username, CancellationToken ct)
    {
        var employee = await _repository.GetEmployeeByCodeAsync(code, ct)
            ?? throw new KeyNotFoundException($"Employee with code '{code}' not found.");
        RequireSalaryPermissions(employee.BranchId);

        dto = dto with
        {
            CurrencyCode = SalaryPaymentMetadata.Normalize(dto.CurrencyCode),
            PaymentMethod = SalaryPaymentMetadata.Normalize(dto.PaymentMethod)
        };
        SalaryPaymentMetadata.Validate(dto.CurrencyCode, dto.PaymentMethod);
        await _repository.ValidateSalaryMetadataAsync(dto.CurrencyCode, dto.PaymentMethod, ct);
        EnsureValidAmountAndRange(dto);

        var lines = await ValidateLinesAsync(dto, ct);
        await TruncatePrecedingStructuresAsync(code, dto.EffectiveFrom, username, ct);

        var structure = new SalaryStructure
        {
            EmployeeCode = code,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            BasicSalary = dto.BasicSalary,
            CurrencyCode = dto.CurrencyCode,
            PaymentMethod = dto.PaymentMethod,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };
        foreach (var line in lines)
        {
            structure.Lines.Add(new SalaryStructureLine
            {
                ComponentCode = line.ComponentCode,
                Amount = line.Amount,
                Percent = line.Percent,
                IsActive = true,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            });
        }

        await _repository.AddSalaryStructureAsync(structure, ct);
        await _repository.SaveChangesAsync(ct);

        // Reload so line DTOs carry component names.
        var active = await _repository.GetActiveSalaryStructureAsync(code, dto.EffectiveFrom, ct);
        return EmployeeMappings.ToStructureDetails(active ?? structure);
    }

    private async Task<List<(string ComponentCode, decimal Amount, decimal? Percent)>> ValidateLinesAsync(AssignSalaryStructureDto dto, CancellationToken ct)
    {
        var validated = new List<(string ComponentCode, decimal Amount, decimal? Percent)>();
        foreach (var line in dto.Lines ?? new List<SalaryStructureLineDto>())
        {
            var componentCode = line.ComponentCode?.Trim();
            if (string.IsNullOrWhiteSpace(componentCode) || componentCode.Length > 50)
                throw new ArgumentException("Salary structure component code is required and cannot exceed 50 characters.");

            var component = await _repository.GetSalaryComponentByCodeAsync(componentCode, ct)
                ?? throw new ArgumentException($"Salary component '{componentCode}' does not exist.");

            var amount = SalaryStructureLineRules.ResolveInputAmount(line.Amount, component);
            var candidate = new SalaryStructureLine { ComponentCode = component.ComponentCode, Amount = amount, Percent = line.Percent };
            SalaryStructureLineRules.ValidateAssignment(candidate, component, dto.BasicSalary);

            validated.Add((component.ComponentCode, amount, line.Percent == 0m ? null : line.Percent));
        }
        return validated;
    }

    private async Task TruncatePrecedingStructuresAsync(string code, DateTime effectiveFrom, string username, CancellationToken ct)
    {
        var existing = await _repository.GetSalaryStructuresAsync(code, ct);

        if (existing.Any(s => s.IsActive && s.EffectiveFrom.Date >= effectiveFrom.Date))
            throw new InvalidOperationException("A salary structure already starts on or after this date. Update or remove that future structure before assigning a backdated salary.");

        foreach (var previous in existing.Where(s => s.IsActive && s.EffectiveFrom.Date < effectiveFrom.Date &&
                                                     (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= effectiveFrom.Date)))
        {
            previous.EffectiveTo = effectiveFrom.Date.AddDays(-1);
            previous.UpdateUser = username;
            previous.UpdateDate = DateTime.UtcNow;
        }
    }

    private void RequireSalaryPermissions(long? branchId)
    {
        _fields.Require(EmployeeFieldAccess.EditSalary, branchId);
        _fields.Require(EmployeeFieldAccess.ViewSalary, branchId);
    }

    private static void EnsureValidAmountAndRange(AssignSalaryStructureDto dto)
    {
        if (dto.BasicSalary < 0 || (dto.EffectiveTo.HasValue && dto.EffectiveTo.Value.Date < dto.EffectiveFrom.Date))
            throw new ArgumentException(InvalidRangeMessage);
    }
}
