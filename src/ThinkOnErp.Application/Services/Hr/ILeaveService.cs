using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ILeaveService
{
    // Leave Types
    Task<List<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true);
    Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string code);
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string currentUser);

    // Leave Policies
    Task<List<LeavePolicyDto>> GetPoliciesByLeaveTypeAsync(string leaveTypeCode);
    Task<LeavePolicyDto> CreatePolicyAsync(CreateLeavePolicyDto dto, string currentUser);

    // Balances & Accruals
    Task<List<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null);
    Task<RunAccrualResultDto> RunMonthlyAccrualAsync(int? year = null, string? currentUser = null);

    // Requests & Approvals
    Task<List<LeaveRequestDto>> GetLeaveRequestsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(long id);
    Task<LeaveRequestDto> SubmitRequestAsync(SubmitLeaveRequestDto dto, string currentUser);
    Task<LeaveRequestDto> ApproveRequestAsync(long id, string approvedBy);
    Task<LeaveRequestDto> RejectRequestAsync(long id, string rejectionReason, string rejectedBy);
}
