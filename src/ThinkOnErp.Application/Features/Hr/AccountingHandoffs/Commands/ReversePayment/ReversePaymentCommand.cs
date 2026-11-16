using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReversePayment;

public sealed record ReversePaymentCommand(
    long PaymentId,
    string Reason,
    string ExternalReference,
    string EvidenceReference,
    string User
) : IRequest<ApiResponse<AccountingHandoffPayment>>;

public sealed class ReversePaymentCommandValidator : AbstractValidator<ReversePaymentCommand>
{
    public ReversePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.ExternalReference).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ReversePaymentCommandHandler : IRequestHandler<ReversePaymentCommand, ApiResponse<AccountingHandoffPayment>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ReversePaymentCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<AccountingHandoffPayment>> Handle(ReversePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _handoffs.ReversePaymentAsync(
            request.PaymentId,
            request.Reason,
            request.ExternalReference,
            request.EvidenceReference,
            request.User,
            cancellationToken);

        return ApiResponse<AccountingHandoffPayment>.CreateSuccess(payment);
    }
}
