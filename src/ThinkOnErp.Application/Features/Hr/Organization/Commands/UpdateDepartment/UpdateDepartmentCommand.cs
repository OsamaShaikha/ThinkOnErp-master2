using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Commands.UpdateDepartment;

public sealed record UpdateDepartmentCommand(string Code, DepartmentWrite Value, string Version, string Username) : IRequest<ApiResponse<HrDepartment>>;

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Department code is required.");
        RuleFor(x => x.Value).NotNull().WithMessage("Department payload is required.");
        RuleFor(x => x.Version).NotEmpty().WithMessage("If-Match version header is required.");
    }
}

public sealed class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, ApiResponse<HrDepartment>>
{
    private readonly IOrganizationRepository _organization;

    public UpdateDepartmentCommandHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrDepartment>> Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        try
        {
            var expectedVersion = OrganizationCatalogRules.ExpectedVersion(request.Version);
            var result = await _organization.WriteDepartmentAsync(request.Code, request.Value, expectedVersion, request.Username, ct);
            return ApiResponse<HrDepartment>.CreateSuccess(result, "Department updated successfully.");
        }
        catch (OrganizationVersionConflictException ex)
        {
            return ApiResponse<HrDepartment>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
