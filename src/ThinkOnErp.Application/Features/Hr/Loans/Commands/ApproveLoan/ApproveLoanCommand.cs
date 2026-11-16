using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveLoan;

public sealed record ApproveLoanCommand(
    long Id,
    string Username
) : IRequest<ApiResponse<EmployeeLoan>>;

public sealed class ApproveLoanCommandValidator : AbstractValidator<ApproveLoanCommand>
{
    public ApproveLoanCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class ApproveLoanCommandHandler : IRequestHandler<ApproveLoanCommand, ApiResponse<EmployeeLoan>>
{
    private readonly ILoanRepository _loanRepository;
    private readonly IEmployeeRepository? _employeeRepository;

    public ApproveLoanCommandHandler(
        ILoanRepository loanRepository,
        IEmployeeRepository? employeeRepository = null)
    {
        _loanRepository = loanRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<ApiResponse<EmployeeLoan>> Handle(ApproveLoanCommand request, CancellationToken cancellationToken)
    {
        var loan = await _loanRepository.GetLoanByIdAsync(request.Id, cancellationToken);
        if (loan == null) throw new KeyNotFoundException($"Loan with ID {request.Id} not found.");
        if (loan.Status == "APPROVED")
        {
            if (string.IsNullOrWhiteSpace(loan.CurrencyCode))
                throw new InvalidOperationException("An explicit currency is required before approving or funding this loan. Cancel and resubmit the request with its currency.");
            await _loanRepository.GetCurrencyRulesForEmployeeAsync(loan.EmployeeCode,
                SalaryPaymentMetadata.Normalize(loan.CurrencyCode), cancellationToken);
            return ApiResponse<EmployeeLoan>.CreateSuccess(loan, "Loan request approved by HR; Accounting disbursement confirmation is still required.");
        }
        if (loan.Status != "PENDING") throw new InvalidOperationException("Only a pending loan request can be approved by HR.");
        var loanEmployeeUser = _employeeRepository == null ? null :
            await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(loan.EmployeeCode, cancellationToken);
        HrApprovalGuard.EnsureNotSelfApproved(request.Username, loan.CreationUser, loanEmployeeUser, "loan request");
        if (string.IsNullOrWhiteSpace(loan.CurrencyCode))
            throw new InvalidOperationException("An explicit currency is required before approving or funding this loan. Cancel and resubmit the request with its currency.");
        await LoanCommon.EnsureCurrencyAmountPrecisionAsync(_loanRepository, loan.EmployeeCode, loan.CurrencyCode, loan.PrincipalAmount, cancellationToken);

        loan.Status = "APPROVED";
        loan.ApprovedBy = request.Username;
        loan.ApprovalDate = DateTime.UtcNow;
        loan.UpdateUser = request.Username;
        loan.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateLoanAsync(loan, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeLoan>.CreateSuccess(loan, "Loan request approved by HR; Accounting disbursement confirmation is still required.");
    }
}
