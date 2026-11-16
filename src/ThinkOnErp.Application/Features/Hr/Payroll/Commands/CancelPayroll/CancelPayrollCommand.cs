using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.CancelPayroll;

public sealed record CancelPayrollCommand(
    long Id,
    CancelPayrollRunDto Dto,
    string User
) : IRequest<ApiResponse<PayrollCalculationResultDto>>;

public sealed class CancelPayrollCommandValidator : AbstractValidator<CancelPayrollCommand>
{
    public CancelPayrollCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length <= 1000)
            .WithMessage("A cancellation reason of at most 1000 characters is required.");
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class CancelPayrollCommandHandler : IRequestHandler<CancelPayrollCommand, ApiResponse<PayrollCalculationResultDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CancelPayrollCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollCalculationResultDto>> Handle(CancelPayrollCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        if (string.IsNullOrWhiteSpace(request.Dto.Reason) || request.Dto.Reason.Trim().Length > 1000)
            throw new ArgumentException("A cancellation reason of at most 1000 characters is required.");

        var reason = request.Dto.Reason.Trim();

        PayrollCalculationResultDto result;
        if (_unitOfWork == null)
        {
            result = await CancelPayrollRunCoreAsync(request.Id, reason, request.User, cancellationToken);
        }
        else
        {
            var snapshot = await _payrollPeriodRepo.GetPayrollRunSnapshotAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Payroll run {request.Id} not found.");
            var period = await _payrollPeriodRepo.GetPeriodForRunAsync(snapshot, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period was not found.");
            result = await _unitOfWork.ExecuteForPayrollPeriodAsync(period.CompanyId, period.PeriodCode,
                () => CancelPayrollRunCoreAsync(request.Id, reason, request.User, cancellationToken), cancellationToken);
        }

        return ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll calculation cancelled; its history is retained.");
    }

    private async Task<PayrollCalculationResultDto> CancelPayrollRunCoreAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken)
    {
        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        if (run.Status == "CANCELLED") return PayrollCommon.MapToResultDto(run);
        if (run.Status is not ("DRAFT" or "CALCULATED") || run.JournalVoucherId.HasValue || run.ApprovalDate.HasValue)
            throw new InvalidOperationException("Only draft or calculated payroll without approval or accounting effects can be cancelled. Reverse approved payroll through its financial workflow first.");
        if (run.Lines.Any(l => l.Status is not ("DRAFT" or "CALCULATED")))
            throw new InvalidOperationException("One or more payroll lines are already processed. Resolve their financial state before cancellation.");
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(run, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        if (period.Status == "CLOSED")
            throw new InvalidOperationException("Reopen the closed payroll period before cancelling this calculation.");
        if (await _payrollPeriodRepo.HasPayrollFinancialLinksAsync(run.Id, cancellationToken))
            throw new InvalidOperationException("This run has linked loan or adjustment settlements. Resolve those financial dependencies before cancellation.");

        run.Status = "CANCELLED";
        run.CancelledBy = user;
        run.CancellationDate = DateTime.UtcNow;
        run.CancellationReason = reason;
        run.UpdateUser = user;
        run.UpdateDate = run.CancellationDate;
        foreach (var line in run.Lines) line.Status = "CANCELLED";
        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        return PayrollCommon.MapToResultDto(run);
    }
}
