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

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.ApproveSettlement;

public sealed record ApproveSettlementCommand(long SettlementId, string Username) : IRequest<ApiResponse<EndOfServiceSettlementDto>>;

public sealed class ApproveSettlementCommandValidator : AbstractValidator<ApproveSettlementCommand>
{
    public ApproveSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementId).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class ApproveSettlementCommandHandler : IRequestHandler<ApproveSettlementCommand, ApiResponse<EndOfServiceSettlementDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEndOfServiceTransactionRunner _transactionRunner;
    private readonly IHrUnitOfWork _unitOfWork;

    public ApproveSettlementCommandHandler(
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

    public async Task<ApiResponse<EndOfServiceSettlementDto>> Handle(ApproveSettlementCommand request, CancellationToken ct)
    {
        var snapshot = await _eosRepo.GetByIdAsync(request.SettlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {request.SettlementId} not found.");

        var dto = await _unitOfWork.ExecuteForEmployeeDatesAsync(
            snapshot.EmployeeCode,
            snapshot.LastWorkingDate,
            DateTime.MaxValue.Date,
            () => _transactionRunner.ExecuteWithSettlementLockAsync(
                request.SettlementId,
                () => ApproveSettlementCoreAsync(request.SettlementId, request.Username, snapshot.EmployeeCode, snapshot.LastWorkingDate, ct),
                ct),
            ct);

        return ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(
            dto,
            "End of service settlement approved successfully and employee status updated to TERMINATED.");
    }

    private async Task<EndOfServiceSettlementDto> ApproveSettlementCoreAsync(
        long settlementId,
        string user,
        string expectedEmployeeCode,
        DateTime expectedLastWorkingDate,
        CancellationToken ct)
    {
        var settlement = await _eosRepo.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");

        if (settlement.Status != EndOfServiceSettlementStatus.Draft)
            throw new InvalidOperationException($"Cannot approve end of service settlement in status '{settlement.Status}'. Only DRAFT settlements can be approved.");

        EnsureSettlementSnapshotUnchanged(settlement, expectedEmployeeCode, expectedLastWorkingDate);

        await _transactionRunner.LockEmployeeForCurrentSettlementAsync(settlement.EmployeeCode, ct);

        var employee = await _employeeRepo.GetEmployeeByCodeAsync(settlement.EmployeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee '{settlement.EmployeeCode}' not found.");

        var employeeUserName = await _employeeRepo.GetLinkedUserNameByEmployeeCodeAsync(settlement.EmployeeCode, ct);
        HrApprovalGuard.EnsureNotSelfApproved(user, settlement.CreationUser, employeeUserName, "end of service settlement");

        if (employee.EmploymentStatus != "ACTIVE" || !employee.IsActive || employee.TerminationDate.HasValue)
            throw new InvalidOperationException("Only an active employee without a prior termination can be approved for end of service. Reconcile the employee state first.");

        settlement.Status = EndOfServiceSettlementStatus.Approved;
        settlement.ApprovedBy = user;
        settlement.ApprovedAtUtc = DateTime.UtcNow;
        settlement.UpdateUser = user;
        settlement.UpdateDate = DateTime.UtcNow;

        // Apply EOS-05 operational changes
        employee.TerminationDate = settlement.LastWorkingDate;
        employee.TerminationReason = settlement.TerminationType;
        employee.EmploymentStatus = "TERMINATED";
        employee.IsActive = false;
        employee.UpdateUser = user;
        employee.UpdateDate = DateTime.UtcNow;

        await _eosRepo.UpdateAsync(settlement, ct);
        await _employeeRepo.UpdateEmployeeAsync(employee, ct);
        await _eosRepo.SaveChangesAsync(ct);

        return EndOfServiceMappings.ToDto(settlement);
    }

    private static void EnsureSettlementSnapshotUnchanged(
        EndOfServiceSettlement settlement,
        string expectedEmployeeCode,
        DateTime expectedLastWorkingDate)
    {
        if (!string.Equals(settlement.EmployeeCode, expectedEmployeeCode, StringComparison.OrdinalIgnoreCase) ||
            settlement.LastWorkingDate.Date != expectedLastWorkingDate.Date)
        {
            throw new InvalidOperationException("The end of service employee, last working date, or status changed while locks were being acquired. Reload the settlement and retry.");
        }
    }
}
