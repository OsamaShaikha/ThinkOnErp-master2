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
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/work-calendars")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class WorkCalendarsController : ControllerBase
{
    private readonly IWorkCalendarService _calendarService;
    private readonly ILogger<WorkCalendarsController> _logger;

    public WorkCalendarsController(
        IWorkCalendarService calendarService,
        ILogger<WorkCalendarsController> logger)
    {
        _calendarService = calendarService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkCalendar>>> CreateCalendar(
        [FromBody] CreateWorkCalendarDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _calendarService.CreateWorkCalendarAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<WorkCalendar>.CreateSuccess(result, "Work calendar created successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WorkCalendar>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkCalendar>>>> GetCalendars(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        var result = await _calendarService.GetCalendarsAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkCalendar>>.CreateSuccess(result, "Calendars retrieved successfully."));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkCalendar>>> GetCalendarById(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _calendarService.GetCalendarByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(ApiResponse<WorkCalendar>.CreateFailure("Work calendar not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<WorkCalendar>.CreateSuccess(result, "Calendar retrieved successfully."));
    }

    [HttpPost("holidays")]
    [ProducesResponseType(typeof(ApiResponse<PublicHoliday>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PublicHoliday>>> CreateHoliday(
        [FromBody] CreatePublicHolidayDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _calendarService.CreateHolidayAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<PublicHoliday>.CreateSuccess(result, "Holiday created successfully."));
    }

    [HttpGet("holidays")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PublicHoliday>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicHoliday>>>> GetHolidays(
        [FromQuery] long companyId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _calendarService.GetHolidaysAsync(companyId, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PublicHoliday>>.CreateSuccess(result, "Holidays retrieved successfully."));
    }

    [HttpGet("working-days-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> CountWorkingDays(
        [FromQuery] long companyId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var count = await _calendarService.CountWorkingDaysAsync(companyId, startDate, endDate, cancellationToken);
        return Ok(ApiResponse<int>.CreateSuccess(count, "Working days counted successfully."));
    }
}
