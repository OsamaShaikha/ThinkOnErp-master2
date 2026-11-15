using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleLoanEarly;

public sealed record SettleLoanEarlyCommand(
    long Id,
    SettleLoanEarlyDto Dto,
    string Username
) : IRequest<ApiResponse<EmployeeLoan>>;

public sealed class SettleLoanEarlyCommandValidator : AbstractValidator<SettleLoanEarlyCommand>
{
    public SettleLoanEarlyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.SettleAmount).GreaterThan(0m);
    }
}

public sealed class SettleLoanEarlyCommandHandler : IRequestHandler<SettleLoanEarlyCommand, ApiResponse<EmployeeLoan>>
{
    private readonly ILoanRepository _loanRepository;

    public SettleLoanEarlyCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<EmployeeLoan>> Handle(SettleLoanEarlyCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.SettleAmount <= 0)
            throw new ArgumentException("Settle amount must be greater than zero.", nameof(dto.SettleAmount));

        var loan = await _loanRepository.GetLoanByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan {request.Id} not found.");

        if (loan.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot settle early a loan in status '{loan.Status}'. Only ACTIVE loans can be settled.");

        await LoanCommon.EnsureCurrencyAmountPrecisionAsync(_loanRepository, loan.EmployeeCode, loan.CurrencyCode, dto.SettleAmount, cancellationToken);

        if (loan.Schedules.Any(s => s.ReservedByHandoffId != null))
            throw new InvalidOperationException("Cannot settle loan early while installments are reserved by an in-flight payroll handoff.");

        if (dto.SettleAmount > loan.RemainingBalance)
            throw new InvalidOperationException($"Settle amount {dto.SettleAmount} cannot exceed remaining loan balance {loan.RemainingBalance}.");

        loan.TotalPaidAmount += dto.SettleAmount;
        loan.RemainingBalance -= dto.SettleAmount;

        decimal remainingToApply = dto.SettleAmount;
        foreach (var s in loan.Schedules.Where(s => s.Status is "PENDING" or "PARTIAL" or "SKIPPED").OrderBy(s => s.InstallmentNo))
        {
            if (remainingToApply <= 0) break;
            decimal needed = s.ScheduledAmount - s.PaidAmount;
            if (remainingToApply >= needed)
            {
                s.PaidAmount = s.ScheduledAmount;
                s.Status = "PAID";
                s.PaidDate = dto.SettleDate ?? DateTime.UtcNow;
                remainingToApply -= needed;
            }
            else
            {
                s.PaidAmount += remainingToApply;
                s.Status = "PARTIAL";
                remainingToApply = 0;
            }
        }

        if (loan.RemainingBalance <= 0)
        {
            loan.Status = "SETTLED";
            loan.EndDate = dto.SettleDate ?? DateTime.UtcNow;
        }

        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[EARLY SETTLEMENT: {dto.SettleAmount} {loan.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]"
            : $"{loan.Notes}; [EARLY SETTLEMENT: {dto.SettleAmount} {loan.CurrencyCode}. Ref: {dto.PaymentReference}. Notes: {dto.Notes}]";
        loan.UpdateUser = request.Username;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeLoan>.CreateSuccess(loan, "Loan settled early successfully.");
    }
}
