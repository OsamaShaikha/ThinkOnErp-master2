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
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
    }

    [HttpGet("shifts")]
    [ProducesResponseType(typeof(List<ShiftScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShiftScheduleDto>>> GetAllShifts([FromQuery] bool activeOnly = true)
    {
        var result = await _attendanceService.GetAllShiftsAsync(activeOnly);
        return Ok(result);
    }

    [HttpGet("shifts/{shiftCode}")]
    [ProducesResponseType(typeof(ShiftScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftScheduleDto>> GetShiftByCode(string shiftCode)
    {
        var shift = await _attendanceService.GetShiftByCodeAsync(shiftCode);
        if (shift == null)
        {
            return NotFound(new { message = $"Shift '{shiftCode}' not found." });
        }
        return Ok(shift);
    }

    [HttpPost("shifts")]
    [ProducesResponseType(typeof(ShiftScheduleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftScheduleDto>> CreateShift([FromBody] CreateShiftScheduleDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _attendanceService.CreateShiftAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetShiftByCode), new { shiftCode = created.ShiftCode }, created);
    }

    [HttpPut("shifts/{shiftCode}")]
    [ProducesResponseType(typeof(ShiftScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftScheduleDto>> UpdateShift(string shiftCode, [FromBody] CreateShiftScheduleDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var updated = await _attendanceService.UpdateShiftAsync(shiftCode, dto, currentUser);
        return Ok(updated);
    }

    [HttpPost("shifts/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignShift([FromBody] AssignShiftDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        await _attendanceService.AssignShiftToEmployeeAsync(dto, currentUser);
        return Ok(new { message = "Shift assignment saved successfully." });
    }

    [HttpPost("attendance/clock-in")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceRecordDto>> ClockIn([FromBody] ClockInRequestDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var record = await _attendanceService.ClockInAsync(dto, currentUser);
        return Ok(record);
    }

    [HttpPost("attendance/clock-out")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AttendanceRecordDto>> ClockOut([FromBody] ClockOutRequestDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var record = await _attendanceService.ClockOutAsync(dto, currentUser);
        return Ok(record);
    }

    [HttpGet("attendance")]
    [ProducesResponseType(typeof(List<AttendanceRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetAttendance(
        [FromQuery] string? employeeCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var list = await _attendanceService.GetAttendanceRecordsAsync(employeeCode, fromDate, toDate, status);
        return Ok(list);
    }

    [HttpPost("attendance/{id:long}/correct")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceRecordDto>> CorrectAttendance(long id, [FromBody] CorrectAttendanceDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var corrected = await _attendanceService.CorrectAttendanceAsync(id, dto, currentUser);
        return Ok(corrected);
    }

    [HttpGet("attendance/overtime")]
    [ProducesResponseType(typeof(List<OvertimeRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OvertimeRecordDto>>> GetOvertime(
        [FromQuery] string? employeeCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var list = await _attendanceService.GetOvertimeRecordsAsync(employeeCode, fromDate, toDate, status);
        return Ok(list);
    }

    [HttpPost("attendance/overtime")]
    [ProducesResponseType(typeof(OvertimeRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OvertimeRecordDto>> RequestOvertime([FromBody] RequestOvertimeDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _attendanceService.RequestOvertimeAsync(dto, currentUser);
        return Created(string.Empty, created);
    }

    [HttpPost("attendance/overtime/{id:long}/approve")]
    [ProducesResponseType(typeof(OvertimeRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OvertimeRecordDto>> ApproveOvertime(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var approved = await _attendanceService.ApproveOvertimeAsync(id, currentUser);
        return Ok(approved);
    }

    [HttpPost("attendance/overtime/{id:long}/reject")]
    [ProducesResponseType(typeof(OvertimeRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OvertimeRecordDto>> RejectOvertime(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var rejected = await _attendanceService.RejectOvertimeAsync(id, currentUser);
        return Ok(rejected);
    }
}
