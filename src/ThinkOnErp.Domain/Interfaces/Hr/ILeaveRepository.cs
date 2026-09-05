using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ILeaveRepository
{
    // Leave Types
    Task<IReadOnlyList<LeaveType>> GetAllLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<LeaveType?> GetLeaveTypeByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> LeaveTypeCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);
    void UpdateLeaveType(LeaveType leaveType);

    // Leave Policies
    Task<IReadOnlyList<LeavePolicy>> GetPoliciesByLeaveTypeAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeavePolicy>> GetAllActivePoliciesAsync(CancellationToken cancellationToken = default);
    Task<LeavePolicy?> GetPolicyByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddPolicyAsync(LeavePolicy policy, CancellationToken cancellationToken = default);
    void UpdatePolicy(LeavePolicy policy);

    // Leave Balances
    Task<IReadOnlyList<LeaveBalance>> GetBalancesByEmployeeAsync(string employeeCode, int year, CancellationToken cancellationToken = default);
    Task<LeaveBalance?> GetBalanceAsync(string employeeCode, string leaveTypeCode, int year, CancellationToken cancellationToken = default);
    Task AddBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
    void UpdateBalance(LeaveBalance balance);

    // Leave Requests
    Task<IReadOnlyList<LeaveRequest>> GetRequestsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<LeaveRequest?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);
    void UpdateRequest(LeaveRequest request);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
