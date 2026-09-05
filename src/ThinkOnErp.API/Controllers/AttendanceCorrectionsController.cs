using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/attendance-corrections")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class AttendanceCorrectionsController : ControllerBase
{
    private readonly IAttendanceCorrectionService _service;
    private readonly IAttendanceCalculationEngine _calcEngine;

    public AttendanceCorrectionsController(
        IAttendanceCorrectionService service,
        IAttendanceCalculationEngine calcEngine)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _calcEngine = calcEngine ?? throw new ArgumentNullException(nameof(calcEngine));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AttendanceCorrectionRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttendanceCorrectionRequestDto>>> GetRequests(
        [FromQuery] string? employeeCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var result = await _service.GetRequestsAsync(employeeCode, fromDate, toDate, status);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceCorrectionRequestDto>> GetById(long id)
    {
        var result = await _service.GetRequestByIdAsync(id);
        if (result == null) return NotFound(new { message = $"Request #{id} not found." });
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AttendanceCorrectionRequestDto>> Submit([FromBody] CreateAttendanceCorrectionRequestDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _service.SubmitCorrectionAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceCorrectionRequestDto>> Approve(long id, [FromQuery] long companyId = 1)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _service.ApproveCorrectionAsync(id, companyId, currentUser);
        return Ok(result);
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceCorrectionRequestDto>> Reject(long id, [FromBody] RejectAttendanceCorrectionDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _service.RejectCorrectionAsync(id, dto, currentUser);
        return Ok(result);
    }

    [HttpPost("punch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> IngestPunch([FromBody] RawPunchDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        await _service.IngestRawPunchAsync(dto, currentUser);
        return Ok(new { message = "Punch recorded successfully." });
    }

    [HttpPost("process-punches")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPunches([FromQuery] DateTime date, [FromQuery] long companyId = 1)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var count = await _calcEngine.ProcessUnprocessedRawPunchesAsync(date, companyId, currentUser);
        return Ok(new { processedEmployees = count });
    }

    [HttpGet("daily/{employeeCode}")]
    [ProducesResponseType(typeof(List<AttendanceDayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttendanceDayDto>>> GetDailyAttendance(
        string employeeCode,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _service.GetAttendanceDaysAsync(employeeCode, fromDate, toDate);
        return Ok(result);
    }
}
