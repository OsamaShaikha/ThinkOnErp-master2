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
[Route("api/hr/work-calendars")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class WorkCalendarsController : ControllerBase
{
    private readonly IWorkCalendarService _service;

    public WorkCalendarsController(IWorkCalendarService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WorkCalendarDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WorkCalendarDto>>> GetAll([FromQuery] long companyId = 1, [FromQuery] bool activeOnly = true)
    {
        var result = await _service.GetAllCalendarsAsync(companyId, activeOnly);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(WorkCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkCalendarDto>> GetById(long id)
    {
        var result = await _service.GetCalendarByIdAsync(id);
        if (result == null) return NotFound(new { message = $"Calendar #{id} not found." });
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkCalendarDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkCalendarDto>> Create([FromBody] CreateWorkCalendarDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _service.CreateCalendarAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/set-default")]
    [ProducesResponseType(typeof(WorkCalendarDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkCalendarDto>> SetDefault(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _service.SetDefaultCalendarAsync(id, currentUser);
        return Ok(result);
    }

    [HttpGet("holidays")]
    [ProducesResponseType(typeof(List<PublicHolidayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PublicHolidayDto>>> GetHolidays(
        [FromQuery] long companyId = 1,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddYears(-1);
        var to = toDate ?? DateTime.UtcNow.AddYears(1);
        var holidays = await _service.GetHolidaysAsync(companyId, from, to);
        return Ok(holidays);
    }

    [HttpPost("holidays")]
    [ProducesResponseType(typeof(PublicHolidayDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PublicHolidayDto>> CreateHoliday([FromBody] CreatePublicHolidayDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _service.CreateHolidayAsync(dto, currentUser);
        return Created("", created);
    }
}
