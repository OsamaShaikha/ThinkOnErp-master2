using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.FailPayment;

public sealed record FailPaymentCommand(
    long PaymentId,
    string Reason,
    string User
) : IRequest<ApiResponse<AccountingHandoffPayment>>;

public sealed class FailPaymentCommandValidator : AbstractValidator<FailPaymentCommand>
{
    public FailPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class FailPaymentCommandHandler : IRequestHandler<FailPaymentCommand, ApiResponse<AccountingHandoffPayment>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public FailPaymentCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<AccountingHandoffPayment>> Handle(FailPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _handoffs.FailPaymentAsync(request.PaymentId, request.Reason, request.User, cancellationToken);
        return ApiResponse<AccountingHandoffPayment>.CreateSuccess(payment);
    }
}
