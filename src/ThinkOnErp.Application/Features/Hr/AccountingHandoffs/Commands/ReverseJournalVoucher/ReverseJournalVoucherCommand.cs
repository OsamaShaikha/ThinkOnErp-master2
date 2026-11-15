using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReverseJournalVoucher;

public sealed record ReverseJournalVoucherCommand(
    long Id,
    DateOnly ReversalDate,
    string Reason,
    string User
) : IRequest<ApiResponse<ReverseJournalVoucherResultDto>>;

public sealed class ReverseJournalVoucherCommandValidator : AbstractValidator<ReverseJournalVoucherCommand>
{
    public ReverseJournalVoucherCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ReverseJournalVoucherCommandHandler : IRequestHandler<ReverseJournalVoucherCommand, ApiResponse<ReverseJournalVoucherResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public ReverseJournalVoucherCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<ReverseJournalVoucherResultDto>> Handle(ReverseJournalVoucherCommand request, CancellationToken cancellationToken)
    {
        var reversalVoucherId = await _handoffs.ReverseJournalVoucherAsync(
            request.Id,
            request.ReversalDate.ToDateTime(TimeOnly.MinValue),
            request.Reason,
            request.User,
            cancellationToken);

        return ApiResponse<ReverseJournalVoucherResultDto>.CreateSuccess(
            new ReverseJournalVoucherResultDto(request.Id, reversalVoucherId, "REVERSED"));
    }
}
