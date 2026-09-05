using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class SelfServiceController : ControllerBase
{
    private readonly ISelfServiceService _selfService;

    public SelfServiceController(ISelfServiceService selfService)
    {
        _selfService = selfService ?? throw new ArgumentNullException(nameof(selfService));
    }

    private string GetCurrentEmployeeCode()
    {
        // Extracts employee code from claim or username
        var empCode = User.FindFirst("EmployeeCode")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.Identity?.Name
            ?? "EMP-CURRENT";
        return empCode;
    }

    // ==========================================
    // Employee Self-Service (ESS) Endpoints
    // ==========================================

    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetMyProfile()
    {
        var empCode = GetCurrentEmployeeCode();
        var profile = await _selfService.GetMyProfileAsync(empCode);
        if (profile == null)
        {
            return NotFound(new { message = $"Profile for employee '{empCode}' not found." });
        }
        return Ok(profile);
    }

    [HttpGet("me/payslips")]
    [ProducesResponseType(typeof(List<PayslipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PayslipDto>>> GetMyPayslips()
    {
        var empCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyPayslipsAsync(empCode);
        return Ok(list);
    }

    [HttpGet("me/leave-balances")]
    [ProducesResponseType(typeof(List<LeaveBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveBalanceDto>>> GetMyLeaveBalances([FromQuery] int? year = null)
    {
        var empCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyLeaveBalancesAsync(empCode, year);
        return Ok(list);
    }

    [HttpGet("me/leave-requests")]
    [ProducesResponseType(typeof(List<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveRequestDto>>> GetMyLeaveRequests()
    {
        var empCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyLeaveRequestsAsync(empCode);
        return Ok(list);
    }

    [HttpPost("me/leave-requests")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveRequestDto>> SubmitMyLeaveRequest([FromBody] SubmitLeaveRequestDto dto)
    {
        var empCode = GetCurrentEmployeeCode();
        var created = await _selfService.SubmitMyLeaveRequestAsync(empCode, dto);
        return Ok(created);
    }

    [HttpGet("me/attendance")]
    [ProducesResponseType(typeof(List<AttendanceRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetMyAttendance(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var empCode = GetCurrentEmployeeCode();
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;
        var list = await _selfService.GetMyAttendanceHistoryAsync(empCode, from, to);
        return Ok(list);
    }

    [HttpGet("me/assets")]
    [ProducesResponseType(typeof(List<AssetAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssetAssignmentDto>>> GetMyAssignedAssets()
    {
        var empCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyAssignedAssetsAsync(empCode);
        return Ok(list);
    }

    [HttpPost("me/expense-claims")]
    [ProducesResponseType(typeof(ExpenseClaimDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseClaimDto>> SubmitMyExpenseClaim([FromBody] SubmitExpenseClaimDto dto)
    {
        var empCode = GetCurrentEmployeeCode();
        var created = await _selfService.SubmitMyExpenseClaimAsync(empCode, dto);
        return Ok(created);
    }

    // ==========================================
    // Manager Self-Service (MSS) Endpoints
    // ==========================================

    [HttpGet("manager/team")]
    [ProducesResponseType(typeof(List<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeDto>>> GetMyTeamMembers()
    {
        var managerCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyTeamMembersAsync(managerCode);
        return Ok(list);
    }

    [HttpGet("manager/team/pending-leaves")]
    [ProducesResponseType(typeof(List<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveRequestDto>>> GetTeamPendingLeaves()
    {
        var managerCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyTeamPendingLeavesAsync(managerCode);
        return Ok(list);
    }

    [HttpPost("manager/team/leaves/{id:long}/approve")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> ApproveTeamLeave(long id)
    {
        var managerCode = GetCurrentEmployeeCode();
        var result = await _selfService.ApproveTeamLeaveAsync(managerCode, id);
        return Ok(result);
    }

    [HttpPost("manager/team/leaves/{id:long}/reject")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> RejectTeamLeave(long id, [FromBody] RejectLeaveRequest request)
    {
        var managerCode = GetCurrentEmployeeCode();
        var result = await _selfService.RejectTeamLeaveAsync(managerCode, id, request.RejectionReason);
        return Ok(result);
    }

    [HttpGet("manager/team/pending-overtime")]
    [ProducesResponseType(typeof(List<OvertimeRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OvertimeRecordDto>>> GetTeamPendingOvertime()
    {
        var managerCode = GetCurrentEmployeeCode();
        var list = await _selfService.GetMyTeamPendingOvertimeAsync(managerCode);
        return Ok(list);
    }

    [HttpPost("manager/team/overtime/{id:long}/approve")]
    [ProducesResponseType(typeof(OvertimeRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OvertimeRecordDto>> ApproveTeamOvertime(long id)
    {
        var managerCode = GetCurrentEmployeeCode();
        var result = await _selfService.ApproveTeamOvertimeAsync(managerCode, id);
        return Ok(result);
    }
}

public sealed class RejectLeaveRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}
