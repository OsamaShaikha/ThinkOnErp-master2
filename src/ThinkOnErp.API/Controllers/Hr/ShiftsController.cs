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
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDefinition;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitionByCode;
using ThinkOnErp.Application.Features.Hr.Shifts.Queries.GetShiftDefinitions;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/shifts"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class ShiftsController : HrControllerBase
{
    private readonly IMediator _mediator;

    public ShiftsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet, HrPermission("hr-attendance", "view")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionPageDto>>> List(
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
        => FromResponse(await _mediator.Send(new GetShiftDefinitionsQuery(activeOnly, search, page, size), ct));

    [HttpGet("{code}"), HrPermission("hr-attendance", "view")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Get(string code, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetShiftDefinitionByCodeQuery(code), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Create(
        [FromBody] CreateShiftDefinitionDto dto,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateShiftDefinitionCommand(dto, CurrentUsername), ct);
        if (response.Success && response.Data != null)
        {
            Response.Headers.ETag = '"' + response.Data.Version + '"';
            return CreatedAtAction(nameof(Get), new { code = response.Data.ShiftCode }, response);
        }
        return FromResponse(response);
    }

    [HttpPut("{code}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Update(
        string code,
        [FromBody] WriteShiftDefinitionDto dto,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateShiftDefinitionCommand(code, dto, version, CurrentUsername), ct);
        if (response.Success && response.Data != null)
            Response.Headers.ETag = '"' + response.Data.Version + '"';
        return FromResponse(response);
    }

    [HttpDelete("{code}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        string code,
        [FromHeader(Name = "If-Match")] string version,
        CancellationToken ct)
        => FromResponse(await _mediator.Send(new DeleteShiftDefinitionCommand(code, version), ct));
}
