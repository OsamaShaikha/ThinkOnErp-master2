using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.CorrectPayroll;

public sealed record CorrectPayrollCommand(
    long Id,
    string Reason,
    string User
) : IRequest<ApiResponse<PayrollCalculationResultDto>>;

public sealed class CorrectPayrollCommandValidator : AbstractValidator<CorrectPayrollCommand>
{
    public CorrectPayrollCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length is >= 3 and <= 1000)
            .WithMessage("A payroll correction reason of 3..1000 characters is required.");
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class CorrectPayrollCommandHandler : IRequestHandler<CorrectPayrollCommand, ApiResponse<PayrollCalculationResultDto>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPayrollCalculationEngine _calculationEngine;
    private readonly IPayrollValidationService _validationService;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IPayrollInputSnapshotService? _inputSnapshots;
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    public CorrectPayrollCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        IHrUnitOfWork? unitOfWork = null,
        IPayrollInputSnapshotService? inputSnapshots = null,
        IHrAccountingHandoffService? accountingHandoffs = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _calculationEngine = calculationEngine;
        _validationService = validationService;
        _unitOfWork = unitOfWork;
        _inputSnapshots = inputSnapshots;
        _accountingHandoffs = accountingHandoffs;
    }

    public async Task<ApiResponse<PayrollCalculationResultDto>> Handle(CorrectPayrollCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 1000)
            throw new ArgumentException("A payroll correction reason of 3..1000 characters is required.");
        if (_unitOfWork == null || _accountingHandoffs == null)
            throw new InvalidOperationException("Payroll correction requires the period lock and Accounting handoff workflow.");

        var snapshot = await _payrollPeriodRepo.GetPayrollRunSnapshotAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll run {request.Id} not found.");
        var periodSnapshot = await _payrollPeriodRepo.GetPeriodForRunAsync(snapshot, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period was not found.");

        var result = await _unitOfWork.ExecuteForPayrollPeriodAsync(periodSnapshot.CompanyId, periodSnapshot.PeriodCode,
            () => CreatePayrollCorrectionCoreAsync(request.Id, reason, request.User, cancellationToken), cancellationToken);

        return ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "A new payroll correction version was calculated; the prior run remains unchanged.");
    }

    private async Task<PayrollCalculationResultDto> CreatePayrollCorrectionCoreAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken)
    {
        var original = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        if (original.Status is "DRAFT" or "CALCULATED" or "CANCELLED" || !original.ApprovalDate.HasValue)
            throw new InvalidOperationException("Only an approved payroll run can be corrected; draft runs should be recalculated or cancelled.");
        var rootRunId = original.CorrectionRootRunId ?? original.Id;
        var latest = await _payrollPeriodRepo.GetLatestPayrollCorrectionAsync(rootRunId, cancellationToken);
        var followsCancelledDraft = latest?.Status == "CANCELLED" && latest.CorrectionOfRunId == original.Id;
        if (latest == null || latest.Id != original.Id && !followsCancelledDraft)
            throw new InvalidOperationException("Correct the latest payroll version only; older approved versions remain immutable history.");
        var handoff = await _accountingHandoffs!.GetPayrollHandoffStatusAsync(original.Id, cancellationToken)
            ?? throw new InvalidOperationException("Payroll correction requires a saved Accounting handoff.");
        if (handoff.DeliveryStatus is not ("WITHDRAWN" or "RETURNED" or "REVERSED"))
            throw new InvalidOperationException("Accounting must return or withdraw the unposted handoff, or reverse its posted journal and all dependent payments, before HR creates a correction.");
        if (handoff.PayPeriod != original.PayPeriod)
            throw new InvalidOperationException("Accounting handoff period does not match the source payroll run.");
        var period = await _payrollPeriodRepo.GetPeriodForRunAsync(original, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period was not found.");
        if (period.Status != "OPEN")
            throw new InvalidOperationException("Reopen the payroll period through the authorized period workflow before calculating a correction.");
        var nextVersion = latest.CorrectionVersion + 1;
        await _payrollPeriodRepo.SupersedePayrollCorrectionFamilyAsync(rootRunId, user, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        return await PayrollCommon.CalculatePayrollRunCoreAsync(_payrollPeriodRepo, _calculationEngine, _validationService, _inputSnapshots,
            new CreatePayrollRunDto(period.CompanyId, original.PayPeriod, original.BranchId), user,
            cancellationToken, original.Id, rootRunId, nextVersion, reason);
    }
}
