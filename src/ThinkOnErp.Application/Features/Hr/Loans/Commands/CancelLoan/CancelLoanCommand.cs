using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelLoan;

public sealed record CancelLoanCommand(
    long Id,
    string Reason,
    string Username
) : IRequest<ApiResponse<EmployeeLoan>>;

public sealed class CancelLoanCommandValidator : AbstractValidator<CancelLoanCommand>
{
    public CancelLoanCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class CancelLoanCommandHandler : IRequestHandler<CancelLoanCommand, ApiResponse<EmployeeLoan>>
{
    private readonly ILoanRepository _loanRepository;

    public CancelLoanCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<EmployeeLoan>> Handle(CancelLoanCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to cancel a loan.", nameof(request.Reason));

        var loan = await _loanRepository.GetLoanByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan {request.Id} not found.");

        if (loan.Status == "CANCELLED")
            throw new InvalidOperationException("Loan is already cancelled.");

        if (loan.Status is "ACTIVE" or "SETTLED")
            throw new InvalidOperationException($"Cannot cancel loan in status '{loan.Status}'. Active or funded loans must be reversed via Accounting or settled early.");

        if (loan.TotalPaidAmount > 0)
            throw new InvalidOperationException("Cannot cancel a loan that has recorded payments.");

        if (loan.Schedules.Any(s => s.ReservedByHandoffId != null))
            throw new InvalidOperationException("Cannot cancel a loan while installments are reserved by a payroll handoff.");

        loan.Status = "CANCELLED";
        foreach (var s in loan.Schedules)
        {
            if (s.Status == "PENDING")
                s.Status = "CANCELLED";
        }

        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[CANCELLED: {request.Reason}]"
            : $"{loan.Notes}; [CANCELLED: {request.Reason}]";
        loan.UpdateUser = request.Username;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeLoan>.CreateSuccess(loan, "Loan cancelled successfully.");
    }
}
