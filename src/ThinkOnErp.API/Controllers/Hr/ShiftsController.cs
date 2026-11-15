using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/shifts"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class ShiftsController(IShiftDefinitionService service) : ControllerBase
{
    [HttpGet, HrPermission("hr-attendance", "view")]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionPageDto>>> List([FromQuery] bool activeOnly = false,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
        => Ok(ApiResponse<ShiftDefinitionPageDto>.CreateSuccess(await service.ListAsync(activeOnly, search, page, size, ct)));

    [HttpGet("{code}"), HrPermission("hr-attendance", "view")]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Get(string code, CancellationToken ct)
    {
        var result = await service.GetAsync(code, ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return Ok(ApiResponse<ShiftDefinitionDto>.CreateSuccess(result));
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Create([FromBody] CreateShiftDefinitionDto dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(dto, User.Identity?.Name ?? "SYSTEM", ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return CreatedAtAction(nameof(Get), new { code = result.ShiftCode }, ApiResponse<ShiftDefinitionDto>.CreateSuccess(result));
    }

    [HttpPut("{code}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDefinitionDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDefinitionDto>>> Update(string code, [FromBody] WriteShiftDefinitionDto dto,
        [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        try
        {
        var result = await service.UpdateAsync(code, dto, version, User.Identity?.Name ?? "SYSTEM", ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return Ok(ApiResponse<ShiftDefinitionDto>.CreateSuccess(result));
        }
        catch (ShiftVersionConflictException ex)
        {
            return StatusCode(412, ApiResponse<ShiftDefinitionDto>.CreateFailure(ex.Message, statusCode: 412));
        }
    }

    [HttpDelete("{code}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string code, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        try
        {
        await service.DeleteAsync(code, version, ct);
        return Ok(ApiResponse<bool>.CreateSuccess(true));
        }
        catch (ShiftVersionConflictException ex)
        {
            return StatusCode(412, ApiResponse<bool>.CreateFailure(ex.Message, statusCode: 412));
        }
    }
}
