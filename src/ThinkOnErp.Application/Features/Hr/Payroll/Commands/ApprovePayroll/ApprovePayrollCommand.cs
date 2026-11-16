using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.ApprovePayroll;

public sealed record ApprovePayrollCommand(
    long Id,
    string User
) : IRequest<ApiResponse<PayrollRun>>;

public sealed class ApprovePayrollCommandValidator : AbstractValidator<ApprovePayrollCommand>
{
    public ApprovePayrollCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ApprovePayrollCommandHandler : IRequestHandler<ApprovePayrollCommand, ApiResponse<PayrollRun>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPayrollValidationService _validationService;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IPayrollInputSnapshotService? _inputSnapshots;
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    public ApprovePayrollCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollValidationService validationService,
        IHrUnitOfWork? unitOfWork = null,
        IPayrollInputSnapshotService? inputSnapshots = null,
        IHrAccountingHandoffService? accountingHandoffs = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _validationService = validationService;
        _unitOfWork = unitOfWork;
        _inputSnapshots = inputSnapshots;
        _accountingHandoffs = accountingHandoffs;
    }

    public async Task<ApiResponse<PayrollRun>> Handle(ApprovePayrollCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        PayrollRun run;
        if (_unitOfWork == null)
        {
            run = await ApprovePayrollRunAsyncCore(request.Id, request.User, cancellationToken);
        }
        else
        {
            var snapshot = await _payrollPeriodRepo.GetPayrollRunSnapshotAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Payroll run {request.Id} not found.");
            var period = await _payrollPeriodRepo.GetPeriodForRunAsync(snapshot, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period was not found.");
            run = await _unitOfWork.ExecuteForPayrollPeriodAsync(period.CompanyId, period.PeriodCode,
                () => ApprovePayrollRunAsyncCore(request.Id, request.User, cancellationToken), cancellationToken);
        }

        return ApiResponse<PayrollRun>.CreateSuccess(run, "Payroll run approved successfully.");
    }

    private async Task<PayrollRun> ApprovePayrollRunAsyncCore(long payrollRunId, string user, CancellationToken cancellationToken)
    {
        var validation = await _validationService.ValidatePostApprovalAsync(payrollRunId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Payroll approval validation failed: {string.Join(", ", validation.Errors)}");
        }

        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null) throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        HrApprovalGuard.EnsureNotSelfApproved(user, run.CalculatedBy, null, "payroll run");
        HrApprovalGuard.EnsureNotSelfApproved(user, run.CreationUser, null, "payroll run");

        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(run, cancellationToken)
            ?? throw new InvalidOperationException("The payroll period and branch company must exist before approval.");
        if (_inputSnapshots != null)
        {
            if (string.IsNullOrWhiteSpace(run.InputFingerprint))
                throw new InvalidOperationException("This payroll has no input fingerprint. Recalculate it before approval.");
            var currentInputs = await _inputSnapshots.CaptureAsync(period, run.BranchId, cancellationToken);
            if (run.InputFingerprint != currentInputs)
                throw new InvalidOperationException("Payroll inputs changed after calculation. Recalculate payroll before approval.");
        }

        run.Status = "APPROVED";
        run.ApprovedBy = user;
        run.ApprovalDate = DateTime.UtcNow;
        run.UpdateUser = user;
        run.UpdateDate = DateTime.UtcNow;
        await _payrollPeriodRepo.UpdatePayrollRunAsync(run, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        if (_accountingHandoffs != null)
            await _accountingHandoffs.CreateApprovedPayrollHandoffAsync(run.Id, user, cancellationToken);
        return run;
    }
}
