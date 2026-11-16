using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.PreparePayment;

public sealed record PreparePaymentCommand(
    long Id,
    decimal Amount,
    string Currency,
    string Method,
    string IdempotencyKey,
    string User
) : IRequest<ApiResponse<AccountingHandoffPayment>>;

public sealed class PreparePaymentCommandValidator : AbstractValidator<PreparePaymentCommand>
{
    public PreparePaymentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class PreparePaymentCommandHandler : IRequestHandler<PreparePaymentCommand, ApiResponse<AccountingHandoffPayment>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public PreparePaymentCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<AccountingHandoffPayment>> Handle(PreparePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _handoffs.PreparePaymentAsync(
            request.Id,
            request.Amount,
            request.Currency,
            request.Method,
            request.IdempotencyKey,
            request.User,
            cancellationToken);

        return ApiResponse<AccountingHandoffPayment>.CreateSuccess(payment);
    }
}
