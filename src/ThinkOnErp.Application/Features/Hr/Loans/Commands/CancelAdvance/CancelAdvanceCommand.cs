using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelAdvance;

public sealed record CancelAdvanceCommand(
    long Id,
    string Reason,
    string Username
) : IRequest<ApiResponse<EmployeeAdvance>>;

public sealed class CancelAdvanceCommandValidator : AbstractValidator<CancelAdvanceCommand>
{
    public CancelAdvanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class CancelAdvanceCommandHandler : IRequestHandler<CancelAdvanceCommand, ApiResponse<EmployeeAdvance>>
{
    private readonly ILoanRepository _loanRepository;

    public CancelAdvanceCommandHandler(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    public async Task<ApiResponse<EmployeeAdvance>> Handle(CancelAdvanceCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to cancel an advance.", nameof(request.Reason));

        var advance = await _loanRepository.GetAdvanceByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Advance {request.Id} not found.");

        if (advance.Status == "CANCELLED")
            throw new InvalidOperationException("Advance is already cancelled.");

        if (advance.Status is "ACTIVE" or "RECOVERED")
            throw new InvalidOperationException($"Cannot cancel advance in status '{advance.Status}'. Active or funded advances must be reversed via Accounting or settled early.");

        if (advance.DeductedAmount > 0)
            throw new InvalidOperationException("Cannot cancel an advance that has recorded deductions.");

        if (advance.ReservedByHandoffId != null)
            throw new InvalidOperationException("Cannot cancel an advance while it is reserved by a payroll handoff.");

        advance.Status = "CANCELLED";
        advance.Reason = string.IsNullOrWhiteSpace(advance.Reason)
            ? $"[CANCELLED: {request.Reason}]"
            : $"{advance.Reason}; [CANCELLED: {request.Reason}]";
        advance.UpdateUser = request.Username;
        advance.UpdateDate = DateTime.UtcNow;

        await _loanRepository.UpdateAdvanceAsync(advance, cancellationToken);
        await _loanRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmployeeAdvance>.CreateSuccess(advance, "Advance cancelled successfully.");
    }
}
