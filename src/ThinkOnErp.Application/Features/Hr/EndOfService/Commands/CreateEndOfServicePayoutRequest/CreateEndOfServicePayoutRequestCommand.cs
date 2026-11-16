using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CreateEndOfServicePayoutRequest;

public sealed record CreateEndOfServicePayoutRequestCommand(long SettlementId, string Username) : IRequest<ApiResponse<EndOfServicePayoutHandoffDto>>;

public sealed class CreateEndOfServicePayoutRequestCommandValidator : AbstractValidator<CreateEndOfServicePayoutRequestCommand>
{
    public CreateEndOfServicePayoutRequestCommandValidator()
    {
        RuleFor(x => x.SettlementId).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class CreateEndOfServicePayoutRequestCommandHandler : IRequestHandler<CreateEndOfServicePayoutRequestCommand, ApiResponse<EndOfServicePayoutHandoffDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public CreateEndOfServicePayoutRequestCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<EndOfServicePayoutHandoffDto>> Handle(CreateEndOfServicePayoutRequestCommand request, CancellationToken ct)
    {
        var handoff = await _handoffs.CreateApprovedFundingHandoffAsync("EOS", request.SettlementId, request.Username, ct);
        var dto = new EndOfServicePayoutHandoffDto(
            handoff.Id,
            handoff.HandoffReference,
            handoff.DeliveryStatus,
            handoff.Operation,
            handoff.Lines.FirstOrDefault()?.Amount ?? 0m,
            handoff.Lines.FirstOrDefault()?.CurrencyCode
        );

        return ApiResponse<EndOfServicePayoutHandoffDto>.CreateSuccess(
            dto,
            "End of service settlement payout handoff created successfully.");
    }
}
