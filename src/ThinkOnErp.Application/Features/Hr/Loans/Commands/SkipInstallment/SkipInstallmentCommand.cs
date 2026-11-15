using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.SkipInstallment;

public sealed record SkipInstallmentCommand(
    long ScheduleId,
    string Reason,
    string Username
) : IRequest<ApiResponse<LoanRepaymentSchedule>>;

public sealed class SkipInstallmentCommandValidator : AbstractValidator<SkipInstallmentCommand>
{
    public SkipInstallmentCommandValidator()
    {
        RuleFor(x => x.ScheduleId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class SkipInstallmentCommandHandler : IRequestHandler<SkipInstallmentCommand, ApiResponse<LoanRepaymentSchedule>>
{
    private readonly ILoanRepository _loanRepository;

    public SkipInstallmentCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<LoanRepaymentSchedule>> Handle(SkipInstallmentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to skip/defer an installment.", nameof(request.Reason));

        var schedule = await _loanRepository.GetScheduleByIdAsync(request.ScheduleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan repayment schedule {request.ScheduleId} not found.");

        if (schedule.Status != "PENDING")
            throw new InvalidOperationException($"Cannot skip installment in status '{schedule.Status}'. Only PENDING installments can be skipped.");

        if (schedule.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot skip an installment that is currently reserved by a payroll handoff.");

        var loan = schedule.EmployeeLoan
            ?? await _loanRepository.GetLoanByIdAsync(schedule.EmployeeLoanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parent loan {schedule.EmployeeLoanId} not found.");

        if (loan.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot skip installments for loan in status '{loan.Status}'. Only ACTIVE loans can have installments skipped.");

        schedule.Status = "SKIPPED";

        var lastSchedule = loan.Schedules.OrderBy(s => s.InstallmentNo).Last();
        var lastDate = DateTime.TryParseExact(lastSchedule.PayPeriod + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : DateTime.UtcNow;

        var nextPeriodCode = lastDate.AddMonths(1).ToString("yyyy-MM");
        var newInstallmentNo = lastSchedule.InstallmentNo + 1;
        var remainingUnpaid = schedule.ScheduledAmount - schedule.PaidAmount;

        var newSchedule = new LoanRepaymentSchedule
        {
            EmployeeLoanId = loan.Id,
            InstallmentNo = newInstallmentNo,
            PayPeriod = nextPeriodCode,
            ScheduledAmount = remainingUnpaid,
            PaidAmount = 0m,
            CarriedForwardAmount = 0m,
            Status = "PENDING"
        };

        loan.Schedules.Add(newSchedule);
        loan.TotalInstallments = loan.Schedules.Count(s => s.Status != "CANCELLED");
        loan.EndDate = lastDate.AddMonths(2);
        loan.Notes = string.IsNullOrWhiteSpace(loan.Notes)
            ? $"[SKIPPED: Installment {schedule.InstallmentNo} deferred to {nextPeriodCode}. Reason: {request.Reason}]"
            : $"{loan.Notes}; [SKIPPED: Installment {schedule.InstallmentNo} deferred to {nextPeriodCode}. Reason: {request.Reason}]";
        loan.UpdateUser = request.Username;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<LoanRepaymentSchedule>.CreateSuccess(schedule, "Loan installment skipped and deferred successfully.");
    }
}
