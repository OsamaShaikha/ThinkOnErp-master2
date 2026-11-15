using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmPayment;

public sealed record ConfirmPaymentCommand(
    long PaymentId,
    string ExternalReference,
    string EvidenceReference,
    string User
) : IRequest<ApiResponse<AccountingHandoffPayment>>;

public sealed class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.ExternalReference).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, ApiResponse<AccountingHandoffPayment>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ConfirmPaymentCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<AccountingHandoffPayment>> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _handoffs.ConfirmPaymentAsync(
            request.PaymentId,
            request.ExternalReference,
            request.EvidenceReference,
            request.User,
            cancellationToken);

        return ApiResponse<AccountingHandoffPayment>.CreateSuccess(payment);
    }
}
