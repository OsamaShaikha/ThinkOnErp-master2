using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

public record DeviceAttendanceDto(string EmployeeCode, string OccurredAt, string PunchType, string DeviceId, string EventId, string Source = "BIOMETRIC");
public record ProcessRawBatchDto(int Size = 100);
public record IgnoreRawAttendanceDto(string Reason);

[ApiController, Route("api/hr/attendance/raw-events"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class RawAttendanceController(IRawAttendanceProcessingService processing, IAttendanceCalculationEngine engine,
    IAttendanceCorrectionRepository repository) : ControllerBase
{
    [HttpPost, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Ingest([FromBody] DeviceAttendanceDto dto, CancellationToken ct)
    {
        try
        {
            var raw = await engine.IngestPunchAsync(dto.EmployeeCode, ThinkOnErp.Domain.Entities.Hr.ShiftTimeRules.ParseInstant(dto.OccurredAt).UtcDateTime,
                dto.PunchType?.Trim().ToUpperInvariant() ?? string.Empty, dto.DeviceId, dto.Source, dto.EventId, User.Identity?.Name ?? "SYSTEM", ct);
            return Ok(ApiResponse<RawAttendanceDto>.CreateSuccess(RawAttendanceProcessingService.Map(raw)));
        }
        catch (ThinkOnErp.Domain.Entities.Hr.AttendanceEventConflictException ex)
        { return Conflict(ApiResponse<RawAttendanceDto>.CreateFailure(ex.Message, statusCode: 409)); }
    }

    [HttpGet("{id:long}"), HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Get(long id, [FromQuery, BindRequired] string employeeCode, CancellationToken ct)
    {
        var raw = await processing.GetAsync(id, ct);
        if (raw.EmployeeCode != employeeCode) return NotFound(ApiResponse<RawAttendanceDto>.CreateFailure("Raw event not found."));
        return Ok(ApiResponse<RawAttendanceDto>.CreateSuccess(raw));
    }

    [HttpGet("pending"), HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RawAttendanceDto>>>> Pending([FromQuery] int size = 100, CancellationToken ct = default)
    {
        if (size < 1 || size > 100) throw new ArgumentException("Queue size must be 1..100.");
        var rows = await repository.GetUnprocessedRawPunchesAsync(size, ct);
        return Ok(ApiResponse<IReadOnlyList<RawAttendanceDto>>.CreateSuccess(rows.Select(RawAttendanceProcessingService.Map).ToArray()));
    }

    [HttpPost("{id:long}/process"), HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<RawProcessingResult>>> Process(long id, CancellationToken ct)
        => Ok(ApiResponse<RawProcessingResult>.CreateSuccess(await processing.ProcessAsync(id, User.Identity?.Name ?? "SYSTEM", ct)));

    [HttpPost("process-batch"), HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RawProcessingResult>>>> Batch([FromBody] ProcessRawBatchDto dto, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<RawProcessingResult>>.CreateSuccess(await processing.ProcessBatchAsync(dto.Size, User.Identity?.Name ?? "SYSTEM", ct)));

    [HttpPost("{id:long}/ignore"), HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Ignore(long id, [FromBody] IgnoreRawAttendanceDto dto, CancellationToken ct)
        => Ok(ApiResponse<RawAttendanceDto>.CreateSuccess(await processing.IgnoreAsync(id, dto.Reason, User.Identity?.Name ?? "SYSTEM", ct)));
}
