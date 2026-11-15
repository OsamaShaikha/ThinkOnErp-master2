using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.SubmitAdvanceFunding;

public sealed record SubmitAdvanceFundingCommand(
    long Id,
    string Username
) : IRequest<ApiResponse<object>>;

public sealed class SubmitAdvanceFundingCommandValidator : AbstractValidator<SubmitAdvanceFundingCommand>
{
    public SubmitAdvanceFundingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class SubmitAdvanceFundingCommandHandler : IRequestHandler<SubmitAdvanceFundingCommand, ApiResponse<object>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public SubmitAdvanceFundingCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<object>> Handle(SubmitAdvanceFundingCommand request, CancellationToken cancellationToken)
    {
        var result = await _handoffs.CreateApprovedFundingHandoffAsync("ADVANCE", request.Id, request.Username, cancellationToken);
        return ApiResponse<object>.CreateSuccess(result);
    }
}
