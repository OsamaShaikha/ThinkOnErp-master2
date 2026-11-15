using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeBank;

public sealed record UpdateEmployeeBankCommand(string EmployeeCode, UpdateEmployeeBankDto Dto, string Username) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class UpdateEmployeeBankCommandValidator : AbstractValidator<UpdateEmployeeBankCommand>
{
    public UpdateEmployeeBankCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Bank data is required.");
    }
}

public sealed class UpdateEmployeeBankCommandHandler : IRequestHandler<UpdateEmployeeBankCommand, ApiResponse<EmployeeDetailsDto>>
{
    private readonly EmployeeProfileWriter _writer;

    public UpdateEmployeeBankCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
        => _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(UpdateEmployeeBankCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);

        var updated = await _writer.UpdateFieldGroupAsync(request.EmployeeCode, EmployeeFieldAccess.EditBank,
            e => EmployeeProfileWriter.StoredProfile(e) with
            {
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankIban = dto.BankIban
            },
            request.Username, ct);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(updated);
    }
}
