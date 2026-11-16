using System;
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
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftAssignment;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignmentById;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftAssignments;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/employees/{employeeCode}/shift-assignments"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class ShiftAssignmentsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public ShiftAssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet, HrPermission("hr-attendance", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentPageDto>>> List(
        string employeeCode,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
        => FromResponse(await _mediator.Send(new GetShiftAssignmentsQuery(employeeCode, from, to, activeOnly, page, size), ct));

    [HttpGet("{id:long}"), HrPermission("hr-attendance", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> Get(string employeeCode, long id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetShiftAssignmentByIdQuery(employeeCode, id), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> Create(
        string employeeCode,
        [FromBody] WriteShiftAssignmentDto dto,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateShiftAssignmentCommand(employeeCode, dto, CurrentUsername), ct);
        if (response.Success && response.Data != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
            return CreatedAtAction(nameof(Get), new { employeeCode, id = response.Data.Id }, response);
        }
        return FromResponse(response);
    }

    [HttpPut("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ShiftAssignmentDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> Update(
        string employeeCode,
        long id,
        [FromBody] WriteShiftAssignmentDto dto,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateShiftAssignmentCommand(employeeCode, id, dto, version, CurrentUsername), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpDelete("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        string employeeCode,
        long id,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
        => FromResponse(await _mediator.Send(new DeleteShiftAssignmentCommand(employeeCode, id, version), ct));
}
