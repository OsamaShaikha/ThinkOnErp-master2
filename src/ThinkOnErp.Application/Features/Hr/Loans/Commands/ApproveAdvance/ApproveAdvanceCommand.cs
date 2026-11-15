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

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveAdvance;

public sealed record ApproveAdvanceCommand(
    long Id,
    string Username
) : IRequest<ApiResponse<EmployeeAdvance>>;

public sealed class ApproveAdvanceCommandValidator : AbstractValidator<ApproveAdvanceCommand>
{
    public ApproveAdvanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class ApproveAdvanceCommandHandler : IRequestHandler<ApproveAdvanceCommand, ApiResponse<EmployeeAdvance>>
{
    private readonly ILoanRepository _loanRepository;
    private readonly IEmployeeRepository? _employeeRepository;

    public ApproveAdvanceCommandHandler(
        ILoanRepository loanRepository,
        IEmployeeRepository? employeeRepository = null)
    {
        _loanRepository = loanRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<ApiResponse<EmployeeAdvance>> Handle(ApproveAdvanceCommand request, CancellationToken cancellationToken)
    {
        var advance = await _loanRepository.GetAdvanceByIdAsync(request.Id, cancellationToken);
        if (advance == null) throw new KeyNotFoundException($"Advance with ID {request.Id} not found.");
        if (advance.Status == "APPROVED")
        {
            if (string.IsNullOrWhiteSpace(advance.CurrencyCode))
                throw new InvalidOperationException("An explicit currency is required before approving or funding this advance. Cancel and resubmit the request with its currency.");
            await _loanRepository.GetCurrencyRulesForEmployeeAsync(advance.EmployeeCode,
                SalaryPaymentMetadata.Normalize(advance.CurrencyCode), cancellationToken);
            return ApiResponse<EmployeeAdvance>.CreateSuccess(advance, "Advance request approved by HR; Accounting disbursement confirmation is still required.");
        }
        if (advance.Status != "PENDING") throw new InvalidOperationException("Only a pending advance request can be approved by HR.");
        var advanceEmployeeUser = _employeeRepository == null ? null :
            await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(advance.EmployeeCode, cancellationToken);
        HrApprovalGuard.EnsureNotSelfApproved(request.Username, advance.CreationUser, advanceEmployeeUser, "advance request");
        if (string.IsNullOrWhiteSpace(advance.CurrencyCode))
            throw new InvalidOperationException("An explicit currency is required before approving or funding this advance. Cancel and resubmit the request with its currency.");
        await LoanCommon.EnsureCurrencyAmountPrecisionAsync(_loanRepository, advance.EmployeeCode, advance.CurrencyCode, advance.AdvanceAmount, cancellationToken);

        advance.Status = "APPROVED";
        advance.ApprovedBy = request.Username;
        advance.ApprovalDate = DateTime.UtcNow;
        advance.UpdateUser = request.Username;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeAdvance>.CreateSuccess(advance, "Advance request approved by HR; Accounting disbursement confirmation is still required.");
    }
}
