using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class LeaveController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeaveController(ILeaveService leaveService)
    {
        _leaveService = leaveService ?? throw new ArgumentNullException(nameof(leaveService));
    }

    [HttpGet("leave-types")]
    [ProducesResponseType(typeof(List<LeaveTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveTypeDto>>> GetAllLeaveTypes([FromQuery] bool activeOnly = true)
    {
        var list = await _leaveService.GetAllLeaveTypesAsync(activeOnly);
        return Ok(list);
    }

    [HttpGet("leave-types/{code}")]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveTypeDto>> GetLeaveTypeByCode(string code)
    {
        var type = await _leaveService.GetLeaveTypeByCodeAsync(code);
        if (type == null)
        {
            return NotFound(new { message = $"Leave type '{code}' not found." });
        }
        return Ok(type);
    }

    [HttpPost("leave-types")]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveTypeDto>> CreateLeaveType([FromBody] CreateLeaveTypeDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _leaveService.CreateLeaveTypeAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetLeaveTypeByCode), new { code = created.LeaveTypeCode }, created);
    }

    [HttpGet("leave-policies/{leaveTypeCode}")]
    [ProducesResponseType(typeof(List<LeavePolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeavePolicyDto>>> GetPolicies(string leaveTypeCode)
    {
        var list = await _leaveService.GetPoliciesByLeaveTypeAsync(leaveTypeCode);
        return Ok(list);
    }

    [HttpPost("leave-policies")]
    [ProducesResponseType(typeof(LeavePolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePolicyDto>> CreatePolicy([FromBody] CreateLeavePolicyDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _leaveService.CreatePolicyAsync(dto, currentUser);
        return Created(string.Empty, created);
    }

    [HttpGet("employees/{employeeCode}/leave-balance")]
    [ProducesResponseType(typeof(List<LeaveBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveBalanceDto>>> GetEmployeeLeaveBalance(string employeeCode, [FromQuery] int? year = null)
    {
        var balances = await _leaveService.GetEmployeeBalancesAsync(employeeCode, year);
        return Ok(balances);
    }

    [HttpPost("leave-accrual/run")]
    [ProducesResponseType(typeof(RunAccrualResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RunAccrualResultDto>> RunMonthlyAccrual([FromQuery] int? year = null)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _leaveService.RunMonthlyAccrualAsync(year, currentUser);
        return Ok(result);
    }

    [HttpGet("leave-requests")]
    [ProducesResponseType(typeof(List<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaveRequestDto>>> GetLeaveRequests(
        [FromQuery] string? employeeCode = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var list = await _leaveService.GetLeaveRequestsAsync(employeeCode, status, fromDate, toDate);
        return Ok(list);
    }

    [HttpGet("leave-requests/{id:long}")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> GetLeaveRequestById(long id)
    {
        var req = await _leaveService.GetLeaveRequestByIdAsync(id);
        if (req == null)
        {
            return NotFound(new { message = $"Leave request #{id} not found." });
        }
        return Ok(req);
    }

    [HttpPost("leave-requests")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> SubmitLeaveRequest([FromBody] SubmitLeaveRequestDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _leaveService.SubmitRequestAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetLeaveRequestById), new { id = created.Id }, created);
    }

    [HttpPost("leave-requests/{id:long}/approve")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> ApproveLeaveRequest(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var approved = await _leaveService.ApproveRequestAsync(id, currentUser);
        return Ok(approved);
    }

    [HttpPost("leave-requests/{id:long}/reject")]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveRequestDto>> RejectLeaveRequest(long id, [FromBody] RejectLeaveRequestInput input)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var rejected = await _leaveService.RejectRequestAsync(id, input.Reason, currentUser);
        return Ok(rejected);
    }
}

public sealed class RejectLeaveRequestInput
{
    public string Reason { get; set; } = string.Empty;
}
