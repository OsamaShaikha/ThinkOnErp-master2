using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEndOfServiceService
{
    Task<EndOfServiceSettlementDto> CalculateSettlementAsync(CreateEndOfServiceRequest request, string user, long companyId, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto> ApproveSettlementAsync(long settlementId, string user, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto> CancelSettlementAsync(long settlementId, string reason, string user, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto?> GetByIdAsync(long settlementId, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default);
    Task<IReadOnlyList<EndOfServiceSettlementDto>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken ct = default);
    Task<EndOfServiceHandoffStatusDto?> GetHandoffStatusAsync(long settlementId, CancellationToken ct = default);
}

public sealed class EndOfServiceService : IEndOfServiceService
{
    private readonly IEndOfServiceRepository _eosRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEndOfServiceCalculationEngine _engine;
    private readonly IPolicyRepository? _policyRepository;
    private readonly IEndOfServiceTransactionRunner? _transactionRunner;
    private readonly IHrUnitOfWork? _unitOfWork;

    public EndOfServiceService(
        IEndOfServiceRepository eosRepo,
        IEmployeeRepository employeeRepo,
        IEndOfServiceCalculationEngine engine,
        IPolicyRepository? policyRepository = null,
        IEndOfServiceTransactionRunner? transactionRunner = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _eosRepo = eosRepo;
        _employeeRepo = employeeRepo;
        _engine = engine;
        _policyRepository = policyRepository;
        _transactionRunner = transactionRunner;
        _unitOfWork = unitOfWork;
    }

    public async Task<EndOfServiceSettlementDto> CalculateSettlementAsync(CreateEndOfServiceRequest request, string user, long companyId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId), "A valid company ID is required for an end of service calculation.");
        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(request.EmployeeCode));

        var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee '{request.EmployeeCode}' not found.");

        var activeStructure = await _employeeRepo.GetActiveSalaryStructureAsync(employee.EmployeeCode, request.LastWorkingDate, ct)
            ?? throw new InvalidOperationException($"Employee '{request.EmployeeCode}' does not have an active salary structure to calculate end of service.");

        var policy = await _eosRepo.GetActivePolicyAsync(companyId, request.LastWorkingDate, ct)
            ?? new EndOfServicePolicy { CompanyId = companyId };
        if (policy.CompanyId != companyId)
            throw new InvalidOperationException("The end of service policy does not belong to the resolved tenant.");

        decimal leaveBalance = await _eosRepo.GetRemainingLeaveBalanceDaysAsync(employee.EmployeeCode, ct);
        decimal outstandingLoans = await _eosRepo.GetOutstandingLoanBalanceAsync(employee.EmployeeCode, ct);
        decimal outstandingAdvances = await _eosRepo.GetOutstandingAdvanceBalanceAsync(employee.EmployeeCode, ct);
        int? currencyDecimals = null;
        var roundingRule = 1;
        if (_policyRepository != null)
        {
            var configuredDecimals = await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(companyId, ct);
            if (configuredDecimals.TryGetValue(SalaryPaymentMetadata.Normalize(activeStructure.CurrencyCode), out var configured))
                currencyDecimals = configured;
            if (employee.BranchId.HasValue)
            {
                roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(employee.BranchId.Value, ct) ?? 1;
                if (roundingRule is < 1 or > 6)
                    throw new InvalidOperationException($"Branch {employee.BranchId.Value} has an invalid payroll rounding rule.");
            }
        }

        var settlement = _engine.Calculate(
            employee,
            activeStructure,
            policy,
            request.TerminationType,
            request.LastWorkingDate,
            request.NoticeDate,
            request.NoticeCompensationDays,
            request.OtherEntitlements,
            request.OtherDeductions,
            leaveBalance,
            outstandingLoans,
            outstandingAdvances,
            request.CalculationNotes,
            currencyDecimals,
            roundingRule
        );
        settlement.CompanyId = companyId;

        settlement.CreationUser = user;
        settlement.CreationDate = DateTime.UtcNow;

        await _eosRepo.AddAsync(settlement, ct);
        await _eosRepo.SaveChangesAsync(ct);

        return MapToDto(settlement);
    }

    public async Task<EndOfServiceSettlementDto> ApproveSettlementAsync(long settlementId, string user, CancellationToken ct = default)
    {
        if (_transactionRunner == null || _unitOfWork == null)
            return _transactionRunner == null
                ? await ApproveSettlementCoreAsync(settlementId, user, ct)
                : await _transactionRunner.ExecuteWithSettlementLockAsync(settlementId, () => ApproveSettlementCoreAsync(settlementId, user, ct), ct);

        var snapshot = await _eosRepo.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");
        return await _unitOfWork.ExecuteForEmployeeDatesAsync(snapshot.EmployeeCode, snapshot.LastWorkingDate, DateTime.MaxValue.Date,
            () => _transactionRunner.ExecuteWithSettlementLockAsync(settlementId,
                () => ApproveSettlementCoreAsync(settlementId, user, ct, snapshot.EmployeeCode, snapshot.LastWorkingDate), ct), ct);
    }

    private async Task<EndOfServiceSettlementDto> ApproveSettlementCoreAsync(long settlementId, string user, CancellationToken ct,
        string? expectedEmployeeCode = null, DateTime? expectedLastWorkingDate = null)
    {
        var settlement = await _eosRepo.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");

        if (settlement.Status != EndOfServiceSettlementStatus.Draft)
            throw new InvalidOperationException($"Cannot approve end of service settlement in status '{settlement.Status}'. Only DRAFT settlements can be approved.");

        EnsureSettlementSnapshotUnchanged(settlement, expectedEmployeeCode, expectedLastWorkingDate);

        if (_transactionRunner != null)
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

        return MapToDto(settlement);
    }

    public async Task<EndOfServiceSettlementDto> CancelSettlementAsync(long settlementId, string reason, string user, CancellationToken ct = default)
    {
        var cancellationReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(cancellationReason) || cancellationReason.Length > 1000)
            throw new ArgumentException("A cancellation reason between 1 and 1000 characters is required.", nameof(reason));

        if (_transactionRunner == null || _unitOfWork == null)
            return _transactionRunner == null
                ? await CancelSettlementCoreAsync(settlementId, cancellationReason, user, ct)
                : await _transactionRunner.ExecuteWithSettlementLockAsync(settlementId, () => CancelSettlementCoreAsync(settlementId, cancellationReason, user, ct), ct);

        var snapshot = await _eosRepo.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"End of service settlement {settlementId} not found.");
        if (snapshot.Status != EndOfServiceSettlementStatus.Approved)
            return await _transactionRunner.ExecuteWithSettlementLockAsync(settlementId,
                () => CancelSettlementCoreAsync(settlementId, cancellationReason, user, ct, snapshot.EmployeeCode, snapshot.LastWorkingDate, snapshot.Status), ct);

        return await _unitOfWork.ExecuteForEmployeeDatesAsync(snapshot.EmployeeCode, snapshot.LastWorkingDate, DateTime.MaxValue.Date,
            () => _transactionRunner.ExecuteWithSettlementLockAsync(settlementId,
                () => CancelSettlementCoreAsync(settlementId, cancellationReason, user, ct, snapshot.EmployeeCode, snapshot.LastWorkingDate, snapshot.Status), ct), ct);
    }

    private async Task<EndOfServiceSettlementDto> CancelSettlementCoreAsync(long settlementId, string cancellationReason, string user, CancellationToken ct,
        string? expectedEmployeeCode = null, DateTime? expectedLastWorkingDate = null, string? expectedStatus = null)
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
            if (_transactionRunner != null)
                await _transactionRunner.LockEmployeeForCurrentSettlementAsync(settlement.EmployeeCode, ct);
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(settlement.EmployeeCode, ct)
                ?? throw new InvalidOperationException("Approved end of service settlement has no employee record; reconcile the settlement before cancellation.");
            if (employee.TerminationDate?.Date != settlement.LastWorkingDate.Date ||
                !string.Equals(employee.TerminationReason, settlement.TerminationType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(employee.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase) ||
                employee.IsActive)
                throw new InvalidOperationException("Employee termination state no longer matches this settlement; reconcile the employee record before cancellation.");

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

        return MapToDto(settlement);
    }

    public async Task<EndOfServiceSettlementDto?> GetByIdAsync(long settlementId, CancellationToken ct = default)
    {
        var settlement = await _eosRepo.GetByIdAsync(settlementId, ct);
        return settlement == null ? null : MapToDto(settlement);
    }

    public async Task<EndOfServiceSettlementDto?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default)
    {
        var settlement = await _eosRepo.GetByEmployeeCodeAsync(employeeCode, ct);
        return settlement == null ? null : MapToDto(settlement);
    }

    public async Task<IReadOnlyList<EndOfServiceSettlementDto>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken ct = default)
    {
        var list = await _eosRepo.GetListAsync(companyId, branchId, status, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<EndOfServiceHandoffStatusDto?> GetHandoffStatusAsync(long settlementId, CancellationToken ct = default)
    {
        var handoff = await _eosRepo.GetLatestHandoffAsync(settlementId, ct);
        if (handoff == null) return null;
        return new EndOfServiceHandoffStatusDto(
            handoff.Id,
            handoff.HandoffReference,
            handoff.DeliveryStatus,
            handoff.Operation,
            handoff.SourceVersion,
            handoff.CreatedAtUtc
        );
    }

    private static EndOfServiceSettlementDto MapToDto(EndOfServiceSettlement s)
    {
        var empName = s.Employee?.NameLocal ?? (!string.IsNullOrWhiteSpace(s.Employee?.NameEn) ? s.Employee.NameEn : s.EmployeeCode);
        var lines = s.Lines.Select(l => new EndOfServiceLineDto(l.Id, l.ComponentCode, l.ComponentType, l.Amount, l.Description)).ToList();

        return new EndOfServiceSettlementDto(
            s.Id,
            s.CompanyId,
            s.BranchId,
            s.EmployeeCode,
            empName,
            s.TerminationType,
            s.TerminationReason,
            s.NoticeDate,
            s.LastWorkingDate,
            s.SettlementDate,
            s.ServiceYears,
            s.ServiceMonths,
            s.ServiceDays,
            s.BaseSalarySnapshot,
            s.GrossSalarySnapshot,
            s.CurrencyCode,
            s.SeverancePay,
            s.UnpaidSalary,
            s.LeaveEncashment,
            s.UnusedLeaveDays,
            s.NoticePeriodPay,
            s.OtherEntitlements,
            s.TotalEntitlements,
            s.OutstandingLoans,
            s.OutstandingAdvances,
            s.NegativeLeaveDeduction,
            s.OtherDeductions,
            s.TotalDeductions,
            s.NetSettlementAmount,
            s.Status,
            s.CalculationNotes,
            s.ApprovedBy,
            s.ApprovedAtUtc,
            s.CancelledBy,
            s.CancelledAtUtc,
            s.CancellationReason,
            lines
        );
    }

    private static void EnsureSettlementSnapshotUnchanged(EndOfServiceSettlement settlement, string? expectedEmployeeCode,
        DateTime? expectedLastWorkingDate, string? expectedStatus = null)
    {
        if (expectedEmployeeCode != null && (!string.Equals(settlement.EmployeeCode, expectedEmployeeCode, StringComparison.OrdinalIgnoreCase) ||
            settlement.LastWorkingDate.Date != expectedLastWorkingDate?.Date ||
            (expectedStatus != null && !string.Equals(settlement.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("The end of service employee, last working date, or status changed while locks were being acquired. Reload the settlement and retry.");
    }
}
