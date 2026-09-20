using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ILeaveRepository
{
    Task<IReadOnlyList<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<LeaveType?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);

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
    Task AddLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);
    Task UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);

    Task<LeaveBalance?> GetLeaveBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetEmployeeBalancesAsync(string employeeCode, int year, CancellationToken cancellationToken = default);
    Task AddLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
    Task UpdateLeaveBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> GetApprovedUnpaidLeavesInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeavePolicy>> GetActiveLeavePoliciesAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
