using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CancelSettlement;

public sealed record CancelSettlementCommand(long SettlementId, string Reason, string Username) : IRequest<ApiResponse<EndOfServiceSettlementDto>>;

public sealed class CancelSettlementCommandValidator : AbstractValidator<CancelSettlementCommand>
{
    public CancelSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementId).GreaterThan(0);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required to cancel an end of service settlement.")
            .MaximumLength(1000).WithMessage("A cancellation reason between 1 and 1000 characters is required.");
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class CancelSettlementCommandHandler : IRequestHandler<CancelSettlementCommand, ApiResponse<EndOfServiceSettlementDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEndOfServiceTransactionRunner _transactionRunner;
    private readonly IHrUnitOfWork _unitOfWork;

    public CancelSettlementCommandHandler(
        IEndOfServiceRepository eosRepo,
        IEmployeeRepository employeeRepo,
        IEndOfServiceTransactionRunner transactionRunner,
        IHrUnitOfWork unitOfWork)
    {
        _eosRepo = eosRepo;
        _employeeRepo = employeeRepo;
        _transactionRunner = transactionRunner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<EndOfServiceSettlementDto>> Handle(CancelSettlementCommand request, CancellationToken ct)
    {
        var cancellationReason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(cancellationReason) || cancellationReason.Length > 1000)
            throw new ArgumentException("A cancellation reason between 1 and 1000 characters is required.", nameof(request.Reason));

        var snapshot = await _eosRepo.GetByIdAsync(request.SettlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {request.SettlementId} not found.");

        EndOfServiceSettlementDto dto;
        if (snapshot.Status != EndOfServiceSettlementStatus.Approved)
        {
            dto = await _transactionRunner.ExecuteWithSettlementLockAsync(
                request.SettlementId,
                () => CancelSettlementCoreAsync(request.SettlementId, cancellationReason, request.Username, snapshot.EmployeeCode, snapshot.LastWorkingDate, snapshot.Status, ct),
                ct);
        }
        else
        {
            dto = await _unitOfWork.ExecuteForEmployeeDatesAsync(
                snapshot.EmployeeCode,
                snapshot.LastWorkingDate,
                DateTime.MaxValue.Date,
                () => _transactionRunner.ExecuteWithSettlementLockAsync(
                    request.SettlementId,
                    () => CancelSettlementCoreAsync(request.SettlementId, cancellationReason, request.Username, snapshot.EmployeeCode, snapshot.LastWorkingDate, snapshot.Status, ct),
                    ct),
                ct);
        }

        return ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(dto, "End of service settlement cancelled successfully.");
    }

    private async Task<EndOfServiceSettlementDto> CancelSettlementCoreAsync(
        long settlementId,
        string cancellationReason,
        string user,
        string expectedEmployeeCode,
        DateTime expectedLastWorkingDate,
        string expectedStatus,
        CancellationToken ct)
    {
        var settlement = await _eosRepo.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");

        EnsureSettlementSnapshotUnchanged(settlement, expectedEmployeeCode, expectedLastWorkingDate, expectedStatus);

        if (settlement.Status == EndOfServiceSettlementStatus.Cancelled)
            throw new InvalidOperationException("End of service settlement is already cancelled.");

        if (settlement.Status == EndOfServiceSettlementStatus.Posted)
            throw new InvalidOperationException("Cannot cancel a posted end of service settlement. Financial reversibility must be processed via Accounting.");

        if (await _eosRepo.HasActiveAccountingHandoffAsync(settlementId, ct))
            throw new InvalidOperationException("Cannot cancel an end of service settlement with an active accounting handoff. The handoff must be reversed or returned by Accounting first.");

        // Revert operational status if previously approved
        if (settlement.Status == EndOfServiceSettlementStatus.Approved)
        {
            await _transactionRunner.LockEmployeeForCurrentSettlementAsync(settlement.EmployeeCode, ct);

            var employee = await _employeeRepo.GetEmployeeByCodeAsync(settlement.EmployeeCode, ct)
                ?? throw new InvalidOperationException("Approved end of service settlement has no employee record; reconcile the settlement before cancellation.");

            if (employee.TerminationDate?.Date != settlement.LastWorkingDate.Date ||
                !string.Equals(employee.TerminationReason, settlement.TerminationType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(employee.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase) ||
                employee.IsActive)
            {
                throw new InvalidOperationException("Employee termination state no longer matches this settlement; reconcile the employee record before cancellation.");
            }

            employee.TerminationDate = null;
            employee.TerminationReason = null;
            employee.EmploymentStatus = "ACTIVE";
            employee.IsActive = true;
            employee.UpdateUser = user;
            employee.UpdateDate = DateTime.UtcNow;
            await _employeeRepo.UpdateEmployeeAsync(employee, ct);
        }

        settlement.Status = EndOfServiceSettlementStatus.Cancelled;
        settlement.CancelledBy = user;
        var cancelledAt = DateTime.UtcNow;
        settlement.CancelledAtUtc = cancelledAt;
        settlement.CancellationReason = cancellationReason;
        settlement.UpdateUser = user;
        settlement.UpdateDate = cancelledAt;

        await _eosRepo.UpdateAsync(settlement, ct);
        await _eosRepo.SaveChangesAsync(ct);

        return EndOfServiceMappings.ToDto(settlement);
    }

    private static void EnsureSettlementSnapshotUnchanged(
        EndOfServiceSettlement settlement,
        string expectedEmployeeCode,
        DateTime expectedLastWorkingDate,
        string expectedStatus)
    {
        if (!string.Equals(settlement.EmployeeCode, expectedEmployeeCode, StringComparison.OrdinalIgnoreCase) ||
            settlement.LastWorkingDate.Date != expectedLastWorkingDate.Date ||
            !string.Equals(settlement.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The end of service employee, last working date, or status changed while locks were being acquired. Reload the settlement and retry.");
        }
    }
}
