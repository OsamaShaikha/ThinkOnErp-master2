using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreatePublicHoliday;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreateWorkCalendar;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.CountWorkingDays;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetPublicHolidays;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendarById;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendars;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/work-calendars")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class WorkCalendarsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public WorkCalendarsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-work-calendars", "create")]
    public async Task<ActionResult<ApiResponse<WorkCalendar>>> CreateCalendar(
        [FromBody] CreateWorkCalendarDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateWorkCalendarCommand(dto, CurrentUsername), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WorkCalendar>>), StatusCodes.Status200OK)]
    [HrPermission("hr-work-calendars", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkCalendar>>>> GetCalendars(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetWorkCalendarsQuery(companyId), cancellationToken));

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendar>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-work-calendars", "view")]
    public async Task<ActionResult<ApiResponse<WorkCalendar>>> GetCalendarById(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetWorkCalendarByIdQuery(id), cancellationToken));

    [HttpPost("holidays")]
    [ProducesResponseType(typeof(ApiResponse<PublicHoliday>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublicHoliday>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-work-calendars", "create")]
    public async Task<ActionResult<ApiResponse<PublicHoliday>>> CreateHoliday(
        [FromBody] CreatePublicHolidayDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreatePublicHolidayCommand(dto, CurrentUsername), cancellationToken));

    [HttpGet("holidays")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PublicHoliday>>), StatusCodes.Status200OK)]
    [HrPermission("hr-work-calendars", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicHoliday>>>> GetHolidays(
        [FromQuery] long companyId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetPublicHolidaysQuery(companyId, year), cancellationToken));

    [HttpGet("working-days-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [HrPermission("hr-work-calendars", "view")]
    public async Task<ActionResult<ApiResponse<int>>> CountWorkingDays(
        [FromQuery] long companyId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CountWorkingDaysQuery(companyId, startDate, endDate), cancellationToken));
}
