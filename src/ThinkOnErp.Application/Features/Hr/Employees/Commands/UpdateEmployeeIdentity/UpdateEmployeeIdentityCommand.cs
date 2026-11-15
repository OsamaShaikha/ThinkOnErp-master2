using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.UpdateEmployeeIdentity;

public sealed record UpdateEmployeeIdentityCommand(string EmployeeCode, UpdateEmployeeIdentityDto Dto, string Username) : IRequest<ApiResponse<EmployeeDetailsDto>>;

public sealed class UpdateEmployeeIdentityCommandValidator : AbstractValidator<UpdateEmployeeIdentityCommand>
{
    public UpdateEmployeeIdentityCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto).NotNull().WithMessage("Identity data is required.");
    }
}

public sealed class UpdateEmployeeIdentityCommandHandler : IRequestHandler<UpdateEmployeeIdentityCommand, ApiResponse<EmployeeDetailsDto>>
{
    private readonly EmployeeProfileWriter _writer;

    public UpdateEmployeeIdentityCommandHandler(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
        => _writer = new EmployeeProfileWriter(repository, unitOfWork, fields);

    public async Task<ApiResponse<EmployeeDetailsDto>> Handle(UpdateEmployeeIdentityCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        ArgumentNullException.ThrowIfNull(dto);
        var countryCode = CountryCodeRules.NormalizeOptional(dto.CitizenshipCountryCode);

        var updated = await _writer.UpdateFieldGroupAsync(request.EmployeeCode, EmployeeFieldAccess.EditIdentity,
            e => EmployeeProfileWriter.StoredProfile(e) with
            {
                NationalId = dto.NationalId,
                Nationality = dto.Nationality,
                PassportNumber = dto.PassportNumber,
                // null = "leave unchanged"; any other value is the normalized replacement.
                CitizenshipCountryCode = dto.CitizenshipCountryCode == null ? e.CitizenshipCountryCode : countryCode,
                DateOfBirth = dto.DateOfBirth,
                Gender = dto.Gender,
                MaritalStatus = dto.MaritalStatus,
                Email = dto.Email,
                Phone = dto.Phone,
                SscNumber = dto.SscNumber,
                TaxExemptionCount = dto.TaxExemptionCount,
                IsHighRiskRole = dto.IsHighRiskRole
            },
            request.Username, ct);

        return ApiResponse<EmployeeDetailsDto>.CreateSuccess(updated);
    }
}
