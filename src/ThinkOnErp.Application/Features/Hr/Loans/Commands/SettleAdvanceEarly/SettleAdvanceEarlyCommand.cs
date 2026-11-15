using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleAdvanceEarly;

public sealed record SettleAdvanceEarlyCommand(
    long Id,
    SettleAdvanceEarlyDto Dto,
    string Username
) : IRequest<ApiResponse<EmployeeAdvance>>;

public sealed class SettleAdvanceEarlyCommandValidator : AbstractValidator<SettleAdvanceEarlyCommand>
{
    public SettleAdvanceEarlyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.SettleAmount).GreaterThan(0m);
    }
}

public sealed class SettleAdvanceEarlyCommandHandler : IRequestHandler<SettleAdvanceEarlyCommand, ApiResponse<EmployeeAdvance>>
{
    private readonly ILoanRepository _loanRepository;

    public SettleAdvanceEarlyCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<EmployeeAdvance>> Handle(SettleAdvanceEarlyCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.SettleAmount <= 0)
            throw new ArgumentException("Settle amount must be greater than zero.", nameof(dto.SettleAmount));

        var advance = await _loanRepository.GetAdvanceByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Advance {request.Id} not found.");

        if (advance.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot settle early an advance in status '{advance.Status}'. Only ACTIVE advances can be settled.");

        await LoanCommon.EnsureCurrencyAmountPrecisionAsync(_loanRepository, advance.EmployeeCode, advance.CurrencyCode, dto.SettleAmount, cancellationToken);

        if (advance.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot settle advance early while it is reserved by an in-flight payroll handoff.");

        if (dto.SettleAmount > advance.RemainingBalance)
            throw new InvalidOperationException($"Settle amount {dto.SettleAmount} cannot exceed remaining advance balance {advance.RemainingBalance}.");

        advance.DeductedAmount += dto.SettleAmount;
        advance.RemainingBalance -= dto.SettleAmount;

        if (advance.RemainingBalance <= 0)
        {
            advance.Status = "RECOVERED";
        }

        advance.Reason = string.IsNullOrWhiteSpace(advance.Reason)
            ? $"[EARLY SETTLEMENT: {dto.SettleAmount} {advance.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]"
            : $"{advance.Reason}; [EARLY SETTLEMENT: {dto.SettleAmount} {advance.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]";
        advance.UpdateUser = request.Username;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeAdvance>.CreateSuccess(advance, "Advance settled early successfully.");
    }
}
