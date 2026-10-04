using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/leaves")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class LeavesController : ControllerBase
{
    private readonly ILeaveService _leaveService;
    private readonly ILogger<LeavesController> _logger;

    public LeavesController(
        ILeaveService leaveService,
        ILogger<LeavesController> logger)
    {
        _leaveService = leaveService;
        _logger = logger;
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveTypeDto>>>> GetLeaveTypes(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveTypesAsync(activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeaveTypeDto>>.CreateSuccess(result, "Leave types retrieved successfully."));
    }

    [HttpGet("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> GetLeaveTypeByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeaveTypeByCodeAsync(code, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<LeaveTypeDto>.CreateFailure($"Leave type with code '{code}' not found."));
        }
        return Ok(ApiResponse<LeaveTypeDto>.CreateSuccess(result));
    }

    [HttpPost("types")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> CreateLeaveType(
        [FromBody] CreateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateLeaveTypeAsync(dto, user, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LeaveTypeDto>.CreateSuccess(result, "Leave type created successfully."));
    }

    [HttpPut("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<LeaveTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> UpdateLeaveType(
        string code,
        [FromBody] UpdateLeaveTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.UpdateLeaveTypeAsync(code, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveTypeDto>.CreateSuccess(result, "Leave type updated successfully."));
    }

    [HttpDelete("types/{code}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteLeaveType(
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var success = await _leaveService.DeleteLeaveTypeAsync(code, user, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.CreateFailure($"Leave type with code '{code}' not found."));
        }
        return Ok(ApiResponse<bool>.CreateSuccess(true, "Leave type deactivated successfully."));
    }

    [HttpGet("requests")]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<LeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<LeaveRequestDto>>>> GetLeaveRequests(
        [FromQuery] string? employeeCode,
        [FromQuery] string? leaveTypeCode,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _leaveService.GetLeaveRequestsPagedAsync(
            employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize, cancellationToken);

        var paged = new PagedResultDto<LeaveRequestDto>(items, totalCount, pageIndex, pageSize);
        return Ok(ApiResponse<PagedResultDto<LeaveRequestDto>>.CreateSuccess(paged, "Leave requests retrieved successfully."));
    }

    [HttpPost("requests")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> SubmitLeaveRequest(
        [FromBody] SubmitLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.SubmitLeaveRequestAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request submitted successfully."));
    }

    [HttpPost("requests/{id:long}/process")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> ProcessLeaveRequest(
        long id,
        [FromBody] ProcessLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.ProcessLeaveRequestAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request processed successfully."));
    }

    [HttpGet("balances/{employeeCode}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaveBalanceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>> GetEmployeeBalances(
        string employeeCode,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _leaveService.GetEmployeeBalancesAsync(employeeCode, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeaveBalanceDto>>.CreateSuccess(result, "Leave balances retrieved successfully."));
    }

    [HttpPost("balances/grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeaveBalanceDto>>> GrantOrAdjustBalance(
        [FromBody] GrantLeaveBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.GrantOrAdjustBalanceAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveBalanceDto>.CreateSuccess(result, "Leave balance granted/adjusted successfully."));
    }

    [HttpPost("balances/accrue-monthly")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunMonthlyAccrual(
        [FromBody] MonthlyLeaveAccrualDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunMonthlyAccrualAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Monthly leave accrual completed successfully."));
    }

    [HttpPost("balances/yearly-grant")]
    [ProducesResponseType(typeof(ApiResponse<LeaveAccrualResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>> RunYearlyAllocation(
        [FromBody] YearlyLeaveAllocationDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunYearlyAllocationAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveAccrualResultDto>.CreateSuccess(result, "Yearly leave grant completed successfully."));
    }

    [HttpPost("balances/year-end-rollover")]
    [ProducesResponseType(typeof(ApiResponse<LeaveRolloverResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeaveRolloverResultDto>>> RunYearEndRollover(
        [FromBody] YearEndRolloverDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.RunYearEndRolloverAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeaveRolloverResultDto>.CreateSuccess(result, "Year-end leave rollover completed successfully."));
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>> GetLeavePolicies(
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveService.GetLeavePoliciesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>.CreateSuccess(result, "Leave policies retrieved successfully."));
    }

    [HttpPost("policies")]
    [ProducesResponseType(typeof(ApiResponse<LeavePolicyDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LeavePolicyDetailsDto>>> CreateOrUpdateLeavePolicy(
        [FromBody] CreateLeavePolicyDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _leaveService.CreateOrUpdateLeavePolicyAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<LeavePolicyDetailsDto>.CreateSuccess(result, "Leave policy saved successfully."));
    }
}

