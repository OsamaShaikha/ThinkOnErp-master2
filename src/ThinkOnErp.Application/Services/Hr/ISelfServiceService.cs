using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ISelfServiceService
{
    // Employee Self-Service (ESS)
    Task<EmployeeDto?> GetMyProfileAsync(string employeeCode);
    Task<List<PayslipDto>> GetMyPayslipsAsync(string employeeCode);
    Task<List<LeaveBalanceDto>> GetMyLeaveBalancesAsync(string employeeCode, int? year = null);
    Task<List<LeaveRequestDto>> GetMyLeaveRequestsAsync(string employeeCode);
    Task<LeaveRequestDto> SubmitMyLeaveRequestAsync(string employeeCode, SubmitLeaveRequestDto dto);
    Task<List<AttendanceRecordDto>> GetMyAttendanceHistoryAsync(string employeeCode, DateTime fromDate, DateTime toDate);
    Task<List<AssetAssignmentDto>> GetMyAssignedAssetsAsync(string employeeCode);
    Task<ExpenseClaimDto> SubmitMyExpenseClaimAsync(string employeeCode, SubmitExpenseClaimDto dto);

    // Manager Self-Service (MSS)
    Task<List<EmployeeDto>> GetMyTeamMembersAsync(string managerEmployeeCode);
    Task<List<LeaveRequestDto>> GetMyTeamPendingLeavesAsync(string managerEmployeeCode);
    Task<LeaveRequestDto> ApproveTeamLeaveAsync(string managerEmployeeCode, long leaveRequestId);
    Task<LeaveRequestDto> RejectTeamLeaveAsync(string managerEmployeeCode, long leaveRequestId, string rejectionReason);
    Task<List<OvertimeRecordDto>> GetMyTeamPendingOvertimeAsync(string managerEmployeeCode);
    Task<OvertimeRecordDto> ApproveTeamOvertimeAsync(string managerEmployeeCode, long overtimeId);
}
