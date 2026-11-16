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
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessAttendanceCorrection;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.SubmitAttendanceCorrection;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceCorrections;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/attendance-corrections")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendanceCorrectionsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceCorrectionsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequest>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance-corrections", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>> SubmitCorrection(
        [FromBody] AttendanceCorrectionRequestDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SubmitAttendanceCorrectionCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/process")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequest>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequest>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance-corrections", "approve")]
    public async Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>> ProcessCorrection(
        long id,
        [FromBody] ProcessAttendanceCorrectionDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ProcessAttendanceCorrectionCommand(id, dto, CurrentUsername), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>), StatusCodes.Status200OK)]
    [HrPermission("hr-attendance-corrections", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>>> GetCorrections(
        [FromQuery] string? employeeCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetAttendanceCorrectionsQuery(employeeCode, status), cancellationToken));
}
