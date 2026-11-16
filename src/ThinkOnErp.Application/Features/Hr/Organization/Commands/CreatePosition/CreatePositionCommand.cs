using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Organization.Commands.CreatePosition;

public sealed record CreatePositionCommand(string Code, PositionWrite Value, string Username) : IRequest<ApiResponse<HrPosition>>;

public sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Position code is required.");
        RuleFor(x => x.Value).NotNull().WithMessage("Position payload is required.");
    }
}

public sealed class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, ApiResponse<HrPosition>>
{
    private readonly IOrganizationRepository _organization;

    public CreatePositionCommandHandler(IOrganizationRepository organization)
    {
        _organization = organization;
    }

    public async Task<ApiResponse<HrPosition>> Handle(CreatePositionCommand request, CancellationToken ct)
    {
        var result = await _organization.WritePositionAsync(request.Code, request.Value, null, request.Username, ct);
        return ApiResponse<HrPosition>.CreateSuccess(result, "Position created successfully.");
    }
}
