using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReturnHandoff;

public sealed record ReturnHandoffCommand(
    long Id,
    string? Reason,
    string User
) : IRequest<ApiResponse<ReviewHandoffResultDto>>;

public sealed class ReturnHandoffCommandValidator : AbstractValidator<ReturnHandoffCommand>
{
    public ReturnHandoffCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ReturnHandoffCommandHandler : IRequestHandler<ReturnHandoffCommand, ApiResponse<ReviewHandoffResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ReturnHandoffCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<ReviewHandoffResultDto>> Handle(ReturnHandoffCommand request, CancellationToken cancellationToken)
    {
        var handoff = await _handoffs.ReviewAsync(request.Id, false, request.Reason, request.User, cancellationToken);
        var result = new ReviewHandoffResultDto(handoff.Id, handoff.HandoffReference, handoff.DeliveryStatus);
        return ApiResponse<ReviewHandoffResultDto>.CreateSuccess(result);
    }
}
