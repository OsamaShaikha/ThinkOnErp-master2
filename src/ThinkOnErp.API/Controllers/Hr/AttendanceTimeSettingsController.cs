using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

public record InitializeAttendanceTimeDto(string TimeZoneId);

[ApiController, Route("api/hr/attendance/time-settings"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class AttendanceTimeSettingsController(IAttendanceTimeSettingsRepository settings) : ControllerBase
{
    [HttpGet, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendanceTimeSettings?>>> Get(CancellationToken ct)
        => Ok(ApiResponse<AttendanceTimeSettings?>.CreateSuccess(await settings.GetCurrentAsync(ct)));

    [HttpPost, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendanceTimeSettings>>> Initialize([FromBody] InitializeAttendanceTimeDto dto, CancellationToken ct)
        => Ok(ApiResponse<AttendanceTimeSettings>.CreateSuccess(await settings.InitializeAsync(dto.TimeZoneId, User.Identity?.Name ?? "SYSTEM", ct)));
}
