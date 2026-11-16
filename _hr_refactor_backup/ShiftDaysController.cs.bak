using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/employees/{employeeCode}/shift-days"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class ShiftDaysController(IShiftDayService service, IShiftGenerationService generation) : ControllerBase
{
    [HttpGet, HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<ShiftDayPageDto>>> List(string employeeCode,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
        => Ok(ApiResponse<ShiftDayPageDto>.CreateSuccess(await service.ListAsync(employeeCode, from, to, activeOnly, page, size, ct)));

    [HttpGet("{id:long}"), HrPermission("hr-attendance", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Get(string employeeCode, long id, CancellationToken ct)
    {
        var result = await service.GetAsync(employeeCode, id, ct);
        Response.Headers.ETag = '"' + result.Version + '"';
        return Ok(ApiResponse<ShiftDayDto>.CreateSuccess(result));
    }

    [HttpPost, HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Create(string employeeCode, [FromBody] WriteShiftDayDto dto, CancellationToken ct)
    {
        try
        {
            var result = await service.CreateAsync(employeeCode, dto, User.Identity?.Name ?? "SYSTEM", ct);
            Response.Headers.ETag = '"' + result.Version + '"';
            return CreatedAtAction(nameof(Get), new { employeeCode, id = result.Id }, ApiResponse<ShiftDayDto>.CreateSuccess(result));
        }
        catch (ShiftVersionConflictException ex) { return StatusCode(412, ApiResponse<ShiftDayDto>.CreateFailure(ex.Message, statusCode: 412)); }
    }

    [HttpPut("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDayDto>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<ShiftDayDto>>> Update(string employeeCode, long id,
        [FromBody] WriteShiftDayDto dto, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateAsync(employeeCode, id, dto, version, User.Identity?.Name ?? "SYSTEM", ct);
            Response.Headers.ETag = '"' + result.Version + '"';
            return Ok(ApiResponse<ShiftDayDto>.CreateSuccess(result));
        }
        catch (InvalidOperationException ex) when (ex is ShiftVersionConflictException or ShiftDayVersionConflictException)
        { return StatusCode(412, ApiResponse<ShiftDayDto>.CreateFailure(ex.Message, statusCode: 412)); }
    }

    [HttpPost("generate"), HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<ShiftGenerationResult>>> Generate(string employeeCode, [FromBody] GenerateShiftDaysDto dto, CancellationToken ct)
        => Ok(ApiResponse<ShiftGenerationResult>.CreateSuccess(await generation.GenerateAsync(employeeCode, dto, User.Identity?.Name ?? "SYSTEM", ct)));

    [HttpDelete("{id:long}"), HrPermission("hr-attendance", "configure")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string employeeCode, long id, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        try { await service.DeleteAsync(employeeCode, id, version, ct); return Ok(ApiResponse<bool>.CreateSuccess(true)); }
        catch (ShiftDayVersionConflictException ex) { return StatusCode(412, ApiResponse<bool>.CreateFailure(ex.Message, statusCode: 412)); }
    }
}
