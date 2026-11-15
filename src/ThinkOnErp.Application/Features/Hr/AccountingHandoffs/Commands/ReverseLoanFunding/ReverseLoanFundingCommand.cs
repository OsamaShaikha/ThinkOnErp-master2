using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReverseLoanFunding;

public sealed record ReverseLoanFundingCommand(
    long Id,
    string Reason,
    string EventKey,
    string ExternalReference,
    string EvidenceReference,
    string User
) : IRequest<ApiResponse<ReverseLoanFundingResultDto>>;

public sealed class ReverseLoanFundingCommandValidator : AbstractValidator<ReverseLoanFundingCommand>
{
    public ReverseLoanFundingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.EventKey).NotEmpty();
        RuleFor(x => x.ExternalReference).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ReverseLoanFundingCommandHandler : IRequestHandler<ReverseLoanFundingCommand, ApiResponse<ReverseLoanFundingResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ReverseLoanFundingCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<ReverseLoanFundingResultDto>> Handle(ReverseLoanFundingCommand request, CancellationToken cancellationToken)
    {
        var reversalVoucherId = await _handoffs.ReverseLoanFundingAsync(
            request.Id,
            request.Reason,
            request.EventKey,
            request.ExternalReference,
            request.EvidenceReference,
            request.User,
            cancellationToken);

        return ApiResponse<ReverseLoanFundingResultDto>.CreateSuccess(
            new ReverseLoanFundingResultDto(request.Id, reversalVoucherId, "REVERSED"));
    }
}
