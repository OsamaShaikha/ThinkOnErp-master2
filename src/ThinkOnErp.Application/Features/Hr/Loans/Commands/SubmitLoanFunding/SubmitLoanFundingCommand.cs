using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.SubmitLoanFunding;

public sealed record SubmitLoanFundingCommand(
    long Id,
    string Username
) : IRequest<ApiResponse<object>>;

public sealed class SubmitLoanFundingCommandValidator : AbstractValidator<SubmitLoanFundingCommand>
{
    public SubmitLoanFundingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class SubmitLoanFundingCommandHandler : IRequestHandler<SubmitLoanFundingCommand, ApiResponse<object>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public SubmitLoanFundingCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<object>> Handle(SubmitLoanFundingCommand request, CancellationToken cancellationToken)
    {
        var result = await _handoffs.CreateApprovedFundingHandoffAsync("LOAN", request.Id, request.Username, cancellationToken);
        return ApiResponse<object>.CreateSuccess(result);
    }
}
