using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.AcceptHandoff;

public sealed record AcceptHandoffCommand(
    long Id,
    string User
) : IRequest<ApiResponse<ReviewHandoffResultDto>>;

public sealed class AcceptHandoffCommandValidator : AbstractValidator<AcceptHandoffCommand>
{
    public AcceptHandoffCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AcceptHandoffCommandHandler : IRequestHandler<AcceptHandoffCommand, ApiResponse<ReviewHandoffResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public AcceptHandoffCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<ReviewHandoffResultDto>> Handle(AcceptHandoffCommand request, CancellationToken cancellationToken)
    {
        var handoff = await _handoffs.ReviewAsync(request.Id, true, null, request.User, cancellationToken);
        var result = new ReviewHandoffResultDto(handoff.Id, handoff.HandoffReference, handoff.DeliveryStatus);
        return ApiResponse<ReviewHandoffResultDto>.CreateSuccess(result);
    }
}
