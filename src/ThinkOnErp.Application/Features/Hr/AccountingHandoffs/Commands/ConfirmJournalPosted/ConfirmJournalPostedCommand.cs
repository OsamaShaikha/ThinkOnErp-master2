using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmJournalPosted;

public sealed record ConfirmJournalPostedCommand(
    long Id,
    long VoucherId,
    string EventKey,
    string User
) : IRequest<ApiResponse<JournalPostedResultDto>>;

public sealed class ConfirmJournalPostedCommandValidator : AbstractValidator<ConfirmJournalPostedCommand>
{
    public ConfirmJournalPostedCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.VoucherId).GreaterThan(0);
        RuleFor(x => x.EventKey).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ConfirmJournalPostedCommandHandler : IRequestHandler<ConfirmJournalPostedCommand, ApiResponse<JournalPostedResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ConfirmJournalPostedCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<JournalPostedResultDto>> Handle(ConfirmJournalPostedCommand request, CancellationToken cancellationToken)
    {
        await _handoffs.ConfirmJournalPostedAsync(request.Id, request.VoucherId, request.EventKey, request.User, cancellationToken);
        return ApiResponse<JournalPostedResultDto>.CreateSuccess(new JournalPostedResultDto(request.Id, "POSTED"));
    }
}
