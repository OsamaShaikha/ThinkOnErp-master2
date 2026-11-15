using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ILeaveRepository
{
    Task<LeaveBalanceMovement?> GetMovementByKeyAsync(string operationKey, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMovement?> GetMovementByIdAsync(long movementId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveBalanceMovement> Items, int Total)> GetMovementsAsync(string employeeCode, string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default);
    Task RecordMovementAsync(LeaveBalance balance, LeaveBalanceState before, string kind, string operationKey, string source,
        string reason, string actor, string? payloadHash = null, decimal expiredDays = 0, decimal transferredDays = 0, long? reversesId = null, CancellationToken cancellationToken = default);
    Task<bool> HasActiveMovementAfterAsync(long balanceId, long sequenceNo, CancellationToken cancellationToken = default);
    Task<T> ExecuteForLeaveEmployeeAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task EnsureLeaveCycleOpenAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<LeaveType?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);
    Task UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);
    Task DeleteLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<LeaveRequest?> GetLeaveRequestByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveRequest?> GetLeaveRequestSnapshotAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> HasConflictingLeaveAsync(string employeeCode, DateTime startDate, DateTime endDate, long? excludeRequestId = null, CancellationToken cancellationToken = default);
    Task AddLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);
    Task UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);

    Task<LeaveBalance?> GetLeaveBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default);
    Task<LeaveBalance?> GetLeaveBalanceByCycleAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate, CancellationToken cancellationToken = default);
    Task<bool> HasMovementReversalAsync(long movementId, CancellationToken cancellationToken = default);
    Task<decimal> GetPendingLeaveDaysAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate, DateTime cycleEndDate, long? exceptRequestId = null, CancellationToken cancellationToken = default);
    Task<bool> IsLeaveCycleClosedAsync(string employeeCode, string leaveTypeCode, DateTime cycleStartDate, DateTime cycleEndDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetEmployeeBalancesAsync(string employeeCode, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetLeaveBalancesByYearAsync(int year, string? leaveTypeCode = null, CancellationToken cancellationToken = default);
    Task AddLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
    Task UpdateLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
    Task<decimal> ApplyAccrualTargetAsync(LeaveBalance target, string user, CancellationToken cancellationToken = default);
    Task<decimal> ApplyAccrualTargetAsync(LeaveBalance target, int expectedLeaveYearStartMonth, int expectedLeaveYearStartDay, string user, CancellationToken cancellationToken = default);

    Task<HrLeaveSettings> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default);
    Task<bool> HasLeaveBalancesAsync(CancellationToken cancellationToken = default);
    Task SaveHrLeaveSettingsAsync(HrLeaveSettings settings, int expectedLeaveYearStartMonth, int expectedLeaveYearStartDay, CancellationToken cancellationToken = default);
    Task<LeaveYearClose?> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveYearClose?> GetLeaveYearCloseByKeyAsync(string closeKey, CancellationToken cancellationToken = default);
    Task<LeaveYearClose> SaveLeaveYearCloseDraftAsync(LeaveYearClose close, CancellationToken cancellationToken = default);
    Task<LeaveYearClose?> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearClose?> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetAllLeaveBalancesForMigrationAsync(CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigration?> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigration?> GetLeaveBalanceMigrationByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigration> SaveLeaveBalanceMigrationDraftAsync(LeaveBalanceMigration migration, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigration?> ApplyLeaveBalanceMigrationAsync(long id, string user, bool reverse, CancellationToken cancellationToken = default);
    Task<PayrollPeriod?> GetPayrollPeriodForLeaveCashoutAsync(string payPeriod, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlement?> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlement?> GetLeaveCashoutSettlementByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlement> SaveLeaveCashoutSettlementDraftAsync(LeaveCashoutSettlement settlement, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlement?> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlement?> ReverseLeaveCashoutSettlementAsync(long id, string reason, string user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> GetApprovedUnpaidLeavesInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);
    Task<bool> HasPendingLeaveRequestsAsync(string employeeCode, string leaveTypeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeavePolicy>> GetActiveLeavePoliciesAsync(CancellationToken cancellationToken = default, DateTime? effectiveDate = null);
    Task<IReadOnlyList<LeavePolicy>> GetLeavePolicyVersionsAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task<LeavePolicy?> GetLeavePolicyByTypeCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default, DateTime? effectiveDate = null);
    Task<T> ExecuteForLeavePolicyAsync<T>(string leaveTypeCode, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task AddLeavePolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default);
    Task UpdateLeavePolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
