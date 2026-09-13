using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Staff Attendance API: Real-time clock-in/clock-out tracking for cashiers, servers, and floor staff.
/// </summary>
[ApiController]
[Route("api/pos/attendance")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosStaffAttendanceController : ControllerBase
{
    private readonly IPosStaffAttendanceService _service;

    public PosStaffAttendanceController(IPosStaffAttendanceService service)
    {
        _service = service;
    }

    /// <summary>
    /// Clocks in a staff member for an active shift session.
    /// </summary>
    [HttpPost("clock-in")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAttendanceDto>>> ClockIn(
        [FromBody] ClockInDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.ClockInAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Clocks out a staff member, computing total work duration.
    /// </summary>
    [HttpPost("clock-out")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StaffAttendanceDto>>> ClockOut(
        [FromBody] ClockOutDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.ClockOutAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves paginated staff attendance logs with date and user filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<StaffAttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<StaffAttendanceDto>>>> GetAttendance(
        [FromQuery] long branchId,
        [FromQuery] long? userId,
        [FromQuery] long? shiftId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAttendancePagedAsync(branchId, userId, shiftId, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }
}
