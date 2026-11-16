using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Commands.UpdatePosition;

public sealed record UpdatePositionCommand(string Code, PositionWrite Value, string Version, string Username) : IRequest<ApiResponse<HrPosition>>;

public sealed class UpdatePositionCommandValidator : AbstractValidator<UpdatePositionCommand>
{
    public UpdatePositionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Position code is required.");
        RuleFor(x => x.Value).NotNull().WithMessage("Position payload is required.");
        RuleFor(x => x.Version).NotEmpty().WithMessage("If-Match version header is required.");
    }
}

public sealed class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand, ApiResponse<HrPosition>>
{
    private readonly IOrganizationRepository _organization;

    public UpdatePositionCommandHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrPosition>> Handle(UpdatePositionCommand request, CancellationToken ct)
    {
        try
        {
            var expectedVersion = OrganizationCatalogRules.ExpectedVersion(request.Version);
            var result = await _organization.WritePositionAsync(request.Code, request.Value, expectedVersion, request.Username, ct);
            return ApiResponse<HrPosition>.CreateSuccess(result, "Position updated successfully.");
        }
        catch (OrganizationVersionConflictException ex)
        {
            return ApiResponse<HrPosition>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
