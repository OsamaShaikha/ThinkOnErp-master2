using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.CreateJournalVoucher;

public sealed record CreateJournalVoucherCommand(
    long Id,
    string User
) : IRequest<ApiResponse<CreateJournalVoucherResultDto>>;

public sealed class CreateJournalVoucherCommandValidator : AbstractValidator<CreateJournalVoucherCommand>
{
    public CreateJournalVoucherCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class CreateJournalVoucherCommandHandler : IRequestHandler<CreateJournalVoucherCommand, ApiResponse<CreateJournalVoucherResultDto>>
{
    private readonly IHrAccountingHandoffService _handoffs;

    public CreateJournalVoucherCommandHandler(IHrAccountingHandoffService handoffs)
    {
        _handoffs = handoffs;
    }

    public async Task<ApiResponse<CreateJournalVoucherResultDto>> Handle(CreateJournalVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucherId = await _handoffs.CreateJournalVoucherAsync(request.Id, request.User, cancellationToken);
        return ApiResponse<CreateJournalVoucherResultDto>.CreateSuccess(new CreateJournalVoucherResultDto(voucherId));
    }
}
