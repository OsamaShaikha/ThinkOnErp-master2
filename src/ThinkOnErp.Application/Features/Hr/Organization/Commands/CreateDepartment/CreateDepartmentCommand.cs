using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(string Code, DepartmentWrite Value, string Username) : IRequest<ApiResponse<HrDepartment>>;

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Department code is required.");
        RuleFor(x => x.Value).NotNull().WithMessage("Department payload is required.");
    }
}

public sealed class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, ApiResponse<HrDepartment>>
{
    private readonly IOrganizationRepository _organization;

    public CreateDepartmentCommandHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrDepartment>> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var result = await _organization.WriteDepartmentAsync(request.Code, request.Value, null, request.Username, ct);
        return ApiResponse<HrDepartment>.CreateSuccess(result, "Department created successfully.");
    }
}
