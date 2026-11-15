using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.IgnoreRawAttendance;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.IngestDeviceAttendance;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessRawAttendance;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessRawAttendanceBatch;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetPendingRawAttendance;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetRawAttendanceById;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/attendance/raw-events"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class RawAttendanceController : HrControllerBase
{
    private readonly IMediator _mediator;

    public RawAttendanceController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<RawAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RawAttendanceDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Ingest([FromBody] DeviceAttendanceDto dto, CancellationToken ct)
        => FromResponse(await _mediator.Send(new IngestDeviceAttendanceCommand(dto, CurrentUsername), ct));

    [HttpGet("{id:long}"), HrPermission("hr-attendance", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<RawAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Get(long id, [FromQuery, BindRequired] string employeeCode, CancellationToken ct)
        => FromResponse(await _mediator.Send(new GetRawAttendanceByIdQuery(id, employeeCode), ct));

    [HttpGet("pending"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RawAttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RawAttendanceDto>>>> Pending([FromQuery] int size = 100, CancellationToken ct = default)
        => FromResponse(await _mediator.Send(new GetPendingRawAttendanceQuery(size), ct));

    [HttpPost("{id:long}/process"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<RawProcessingResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RawProcessingResult>>> Process(long id, CancellationToken ct)
        => FromResponse(await _mediator.Send(new ProcessRawAttendanceCommand(id, CurrentUsername), ct));

    [HttpPost("process-batch"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RawProcessingResult>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RawProcessingResult>>>> Batch([FromBody] ProcessRawBatchDto dto, CancellationToken ct)
        => FromResponse(await _mediator.Send(new ProcessRawAttendanceBatchCommand(dto, CurrentUsername), ct));

    [HttpPost("{id:long}/ignore"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<RawAttendanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RawAttendanceDto>>> Ignore(long id, [FromBody] IgnoreRawAttendanceDto dto, CancellationToken ct)
        => FromResponse(await _mediator.Send(new IgnoreRawAttendanceCommand(id, dto, CurrentUsername), ct));
}
