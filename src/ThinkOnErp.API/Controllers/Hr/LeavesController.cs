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
}
