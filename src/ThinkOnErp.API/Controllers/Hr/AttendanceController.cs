using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.CheckIn;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.CheckOut;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.SaveDayClassification;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.ClassifyDay;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceDays;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceMonthSummary;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetEffectiveShift;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetNearbyBranches;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetTodayAttendanceStatus;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.MatchPunchShift;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.PreviewUtcShift;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/attendance")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendanceController : HrControllerBase
{
    private readonly IMediator? _mediator;
    private readonly IHrAttendanceService? _attendanceService;
    private readonly ILogger<AttendanceController>? _logger;

    [ActivatorUtilitiesConstructor]
    public AttendanceController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [Obsolete("Transitional constructor for existing test suites. Production uses the IMediator constructor.")]
    public AttendanceController(IHrAttendanceService attendanceService, ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("employees/{employeeCode}/effective-shift")]
    [ProducesResponseType(typeof(ApiResponse<EffectiveShiftDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<EffectiveShiftDto>>> GetEffectiveShift(
        string employeeCode,
        [FromQuery, BindRequired] DateOnly workDate,
        [FromServices] IEffectiveShiftService? shifts = null,
        CancellationToken cancellationToken = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetEffectiveShiftQuery(employeeCode, workDate), cancellationToken));

        var result = await (shifts ?? throw new InvalidOperationException("Shifts service required.")).ResolveAsync(employeeCode, workDate, cancellationToken);
        return Ok(ApiResponse<EffectiveShiftDto>.CreateSuccess(result));
    }

    [HttpGet("employees/{employeeCode}/effective-shift-utc")]
    [ProducesResponseType(typeof(ApiResponse<TimedShiftDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<TimedShiftDto>>> PreviewUtc(
        string employeeCode,
        [FromQuery, BindRequired] DateOnly workDate,
        [FromServices] IShiftTimingService? timing = null,
        CancellationToken ct = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new PreviewUtcShiftQuery(employeeCode, workDate), ct));

        return Ok(ApiResponse<TimedShiftDto>.CreateSuccess(await (timing ?? throw new InvalidOperationException("Timing service required.")).PreviewConfiguredAsync(employeeCode, workDate, ct)));
    }

    [HttpGet("employees/{employeeCode}/match-punch-shift")]
    [ProducesResponseType(typeof(ApiResponse<TimedShiftDto?>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<TimedShiftDto?>>> MatchPunchShift(
        string employeeCode,
        [FromQuery, BindRequired] string instant,
        [FromQuery, BindRequired] string punchType,
        [FromServices] IShiftTimingService? timing = null,
        CancellationToken ct = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new MatchPunchShiftQuery(employeeCode, instant, punchType), ct));

        return Ok(ApiResponse<TimedShiftDto?>.CreateSuccess(await (timing ?? throw new InvalidOperationException("Timing service required.")).MatchConfiguredPunchAsync(
            employeeCode, ThinkOnErp.Domain.Entities.Hr.ShiftTimeRules.ParseInstant(instant), punchType, ct)));
    }

    [HttpGet("employees/{employeeCode}/day-classification")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDayClassificationDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>> ClassifyDay(
        string employeeCode,
        [FromQuery, BindRequired] DateOnly workDate,
        [FromServices] IAttendanceDayStatusService? statuses = null,
        CancellationToken ct = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new ClassifyDayQuery(employeeCode, workDate), ct));

        return Ok(ApiResponse<AttendanceDayClassificationDto>.CreateSuccess(await (statuses ?? throw new InvalidOperationException("Status service required.")).PreviewAsync(employeeCode, workDate, ct)));
    }

    [HttpPost("employees/{employeeCode}/day-classification")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDayClassificationDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>> SaveDayClassification(
        string employeeCode,
        [FromQuery, BindRequired] DateOnly workDate,
        [FromServices] IAttendanceDayStatusService? statuses = null,
        CancellationToken ct = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new SaveDayClassificationCommand(employeeCode, workDate, CurrentUsername), ct));

        return Ok(ApiResponse<AttendanceDayClassificationDto>.CreateSuccess(await (statuses ?? throw new InvalidOperationException("Status service required.")).SaveAsync(employeeCode, workDate, CurrentUsername, ct)));
    }

    /// <summary>
    /// Clock in with GPS coordinates and branch range validation.
    /// </summary>
    [HttpPost("check-in")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status409Conflict)]
    [HrPermission("hr-attendance", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendancePunchResultDto>>> CheckIn(
        [FromBody] CheckInRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CheckInCommand(dto, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        AttendancePunchResultDto result;
        try { result = await _attendanceService!.CheckInAsync(dto, user, cancellationToken); }
        catch (ThinkOnErp.Domain.Entities.Hr.AttendanceEventConflictException ex) { return Conflict(ApiResponse<AttendancePunchResultDto>.CreateFailure(ex.Message, statusCode: 409)); }

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

        if (result.EvidenceOnly) return StatusCode(StatusCodes.Status202Accepted, ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message, 202));
        return Ok(ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message));
    }

    /// <summary>
    /// Clock out with GPS coordinates and branch range validation.
    /// </summary>
    [HttpPost("check-out")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePunchResultDto>), StatusCodes.Status409Conflict)]
    [HrPermission("hr-attendance", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendancePunchResultDto>>> CheckOut(
        [FromBody] CheckOutRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CheckOutCommand(dto, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        AttendancePunchResultDto result;
        try { result = await _attendanceService!.CheckOutAsync(dto, user, cancellationToken); }
        catch (ThinkOnErp.Domain.Entities.Hr.AttendanceEventConflictException ex) { return Conflict(ApiResponse<AttendancePunchResultDto>.CreateFailure(ex.Message, statusCode: 409)); }

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

        if (result.EvidenceOnly) return StatusCode(StatusCodes.Status202Accepted, ApiResponse<AttendancePunchResultDto>.CreateSuccess(result, result.Message, 202));
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
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetTodayAttendanceStatusQuery(employeeCode), cancellationToken));

        var status = await _attendanceService!.GetTodayStatusAsync(employeeCode, cancellationToken);
        if (status == null)
        {
            return NotFound(ApiResponse<TodayAttendanceStatusDto>.CreateFailure("Employee attendance record not found."));
        }

        return Ok(ApiResponse<TodayAttendanceStatusDto>.CreateSuccess(status));
    }

    [HttpGet("days")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>>> GetAttendanceDays(
        [FromQuery, BindRequired] string employeeCode,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetAttendanceDaysQuery(employeeCode, from, to, status), cancellationToken));

        if (from > to || to.DayNumber - from.DayNumber > 366)
            return BadRequest(ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>.CreateFailure(
                "Attendance history range must be valid and no longer than 367 days.", statusCode: StatusCodes.Status400BadRequest));
        var result = await _attendanceService!.GetAttendanceHistoryAsync(employeeCode, from, to, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>.CreateSuccess(result, "Attendance history retrieved successfully."));
    }

    [HttpGet("employees/{employeeCode}/months/{year:int}/{month:int}/summary")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceMonthSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceMonthSummaryDto>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendanceMonthSummaryDto>>> GetAttendanceMonthSummary(
        string employeeCode, int year, int month, CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetAttendanceMonthSummaryQuery(employeeCode, year, month), cancellationToken));

        if (year is < 1 or > 9998 || month is < 1 or > 12)
            return BadRequest(ApiResponse<AttendanceMonthSummaryDto>.CreateFailure(
                "A valid year and month are required.", statusCode: StatusCodes.Status400BadRequest));
        var result = await _attendanceService!.GetAttendanceMonthSummaryAsync(employeeCode, year, month, cancellationToken);
        return Ok(ApiResponse<AttendanceMonthSummaryDto>.CreateSuccess(result, "Attendance month summary retrieved successfully."));
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
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetNearbyBranchesQuery(latitude, longitude, companyId), cancellationToken));

        var branches = await _attendanceService!.GetNearbyBranchesAsync(latitude, longitude, companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NearbyBranchDto>>.CreateSuccess(branches));
    }
}
