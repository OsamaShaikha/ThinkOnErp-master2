using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.InitializeAttendanceTime;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceTimeSettings;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/attendance/time-settings"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class AttendanceTimeSettingsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceTimeSettingsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendanceTimeSettings?>>> Get(CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetAttendanceTimeSettingsQuery(), ct));

    [HttpPost, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendanceTimeSettings>>> Initialize(
        [FromBody] InitializeAttendanceTimeDto dto, CancellationToken ct)
        => FromResponse(await _mediator.Send(new InitializeAttendanceTimeCommand(dto, CurrentUsername), ct));
}
