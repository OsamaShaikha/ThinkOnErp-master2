using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmLoanFunding;

public sealed record ConfirmLoanFundingCommand(
    long Id,
    decimal FundedAmount,
    string Currency,
    long FundingVoucherId,
    string EventKey,
    string ExternalReference,
    string User
) : IRequest<ApiResponse<LoanFundingReceiptResultDto>>;

public sealed class ConfirmLoanFundingCommandValidator : AbstractValidator<ConfirmLoanFundingCommand>
{
    public ConfirmLoanFundingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FundedAmount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.FundingVoucherId).GreaterThan(0);
        RuleFor(x => x.EventKey).NotEmpty();
        RuleFor(x => x.ExternalReference).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ConfirmLoanFundingCommandHandler : IRequestHandler<ConfirmLoanFundingCommand, ApiResponse<LoanFundingReceiptResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ConfirmLoanFundingCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<LoanFundingReceiptResultDto>> Handle(ConfirmLoanFundingCommand request, CancellationToken cancellationToken)
    {
        await _handoffs.ConfirmLoanFundingAsync(
            request.Id,
            request.FundedAmount,
            request.Currency,
            request.FundingVoucherId,
            request.EventKey,
            request.ExternalReference,
            request.User,
            cancellationToken);

        return ApiResponse<LoanFundingReceiptResultDto>.CreateSuccess(
            new LoanFundingReceiptResultDto(request.Id, request.FundingVoucherId, "POSTED"));
    }
}
