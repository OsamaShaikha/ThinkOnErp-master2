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
[Route("api/hr/attendance")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly IHrAttendanceService _attendanceService;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IHrAttendanceService attendanceService,
        ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Clock in with GPS coordinates and branch range validation.
    /// </summary>
    [HttpPost("check-in")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-attendance", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendancePunchResultDto>>> CheckIn(
        [FromBody] CheckInRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _attendanceService.CheckInAsync(dto, user, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiResponse<AttendancePunchResultDto>
            {
                Success = false,
                Message = result.Message,
                Data = result,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }

        return Ok(ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message));
    }

    /// <summary>
    /// Clock out with GPS coordinates and branch range validation.
    /// </summary>
    [HttpPost("check-out")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-attendance", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendancePunchResultDto>>> CheckOut(
        [FromBody] CheckOutRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _attendanceService.CheckOutAsync(dto, user, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiResponse<AttendancePunchResultDto>
            {
                Success = false,
                Message = result.Message,
                Data = result,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }

        return Ok(ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message));
    }

    /// <summary>
    /// Gets today's attendance summary and check-in/out status for an employee.
    /// </summary>
    [HttpGet("today-status")]
    [ProducesResponseType(typeof(ApiResponse<TodayAttendanceStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<TodayAttendanceStatusDto>>> GetTodayStatus(
        [FromQuery] string employeeCode,
        CancellationToken cancellationToken)
    {
        var status = await _attendanceService.GetTodayStatusAsync(employeeCode, cancellationToken);
        if (status == null)
        {
            return NotFound(ApiResponse<TodayAttendanceStatusDto>.CreateFailure("Employee attendance record not found."));
        }

        return Ok(ApiResponse<TodayAttendanceStatusDto>.CreateSuccess(status));
    }

    /// <summary>
    /// Lists company branches sorted by proximity to the specified GPS coordinates, indicating if within range.
    /// </summary>
    [HttpGet("nearby-branches")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NearbyBranchDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NearbyBranchDto>>>> GetNearbyBranches(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] long? companyId,
        CancellationToken cancellationToken)
    {
        var branches = await _attendanceService.GetNearbyBranchesAsync(latitude, longitude, companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NearbyBranchDto>>.CreateSuccess(branches));
    }
}
