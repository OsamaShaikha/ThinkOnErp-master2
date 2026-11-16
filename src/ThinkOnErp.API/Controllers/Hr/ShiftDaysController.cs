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
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.GenerateShiftDays;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDay;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDayById;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDays;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/employees/{employeeCode}/shift-days"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class ShiftDaysController : HrControllerBase
{
    private readonly IMediator _mediator;

    public ShiftDaysController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet, HrPermission("hr-attendance", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShiftDayPageDto>>> List(
        string employeeCode,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
        => FromResponse(await _mediator.Send(new GetShiftDaysQuery(employeeCode, from, to, activeOnly, page, size), ct));

    [HttpGet("{id:long}"), HrPermission("hr-attendance", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Get(string employeeCode, long id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetShiftDayByIdQuery(employeeCode, id), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Create(
        string employeeCode,
        [FromBody] WriteShiftDayDto dto,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateShiftDayCommand(employeeCode, dto, CurrentUsername), ct);
        if (response.Success && response.Data != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
            return CreatedAtAction(nameof(Get), new { employeeCode, id = response.Data.Id }, response);
        }
        return FromResponse(response);
    }

    [HttpPut("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Update(
        string employeeCode,
        long id,
        [FromBody] WriteShiftDayDto dto,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateShiftDayCommand(employeeCode, id, dto, version, CurrentUsername), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPost("generate"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftGenerationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftGenerationResult>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ShiftGenerationResult>>> Generate(
        string employeeCode,
        [FromBody] GenerateShiftDaysDto dto,
        CancellationToken ct)
        => FromResponse(await _mediator.Send(new GenerateShiftDaysCommand(employeeCode, dto, CurrentUsername), ct));

    [HttpDelete("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        string employeeCode,
        long id,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
        => FromResponse(await _mediator.Send(new DeleteShiftDayCommand(employeeCode, id, version), ct));
}
