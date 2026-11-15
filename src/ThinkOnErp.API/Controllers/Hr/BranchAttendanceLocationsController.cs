using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
namespace ThinkOnErp.API.Controllers.Hr;

[ApiController, Route("api/hr/attendance/branches/{branchId:long}/location"), Authorize, TenantScoped]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
public sealed class BranchAttendanceLocationsController(IBranchAttendanceLocationRepository locations) : ControllerBase
{
    [HttpGet, HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<BranchAttendanceLocationDto?>>> Get(long branchId, CancellationToken ct)
    {
        var value = await locations.GetLocationAsync(branchId, ct);
        if (value != null) Response.Headers.ETag = '"' + BranchAttendanceLocationRules.Version(value) + '"';
        return Ok(ApiResponse<BranchAttendanceLocationDto?>.CreateSuccess(value == null ? null : Map(value)));
    }
    [HttpPost, HrPermission("hr-attendance", "configure")]
    public Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Create(long branchId, [FromBody] WriteBranchAttendanceLocationDto dto, CancellationToken ct)
        => Save(branchId, dto, null, ct);
    [HttpPut, HrPermission("hr-attendance", "configure")]
    public Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Update(long branchId, [FromBody] WriteBranchAttendanceLocationDto dto,
        [FromHeader(Name = "If-Match")] string version, CancellationToken ct) => Save(branchId, dto, version, ct);
    private async Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>> Save(long branchId, WriteBranchAttendanceLocationDto dto, string? version, CancellationToken ct)
    {
        var proposed = new BranchAttendanceLocation { BranchId = branchId, Latitude = dto.Latitude, Longitude = dto.Longitude, RadiusMeters = dto.RadiusMeters, EnforceGeofence = dto.EnforceGeofence };
        try
        {
            var saved = await locations.SaveLocationAsync(proposed, version, User.Identity?.Name ?? "SYSTEM", ct);
            Response.Headers.ETag = '"' + BranchAttendanceLocationRules.Version(saved) + '"';
            return Ok(ApiResponse<BranchAttendanceLocationDto>.CreateSuccess(Map(saved)));
        }
        catch (BranchLocationVersionException ex) { return StatusCode(412, ApiResponse<BranchAttendanceLocationDto>.CreateFailure(ex.Message, statusCode: 412)); }
    }
    private static BranchAttendanceLocationDto Map(BranchAttendanceLocation value) => new(value.BranchId, value.CompanyId, value.Latitude, value.Longitude,
        value.RadiusMeters, value.EnforceGeofence, BranchAttendanceLocationRules.Version(value), value.CreationUser, DateTime.SpecifyKind(value.CreationDate, DateTimeKind.Utc),
        value.UpdateUser, value.UpdateDate.HasValue ? DateTime.SpecifyKind(value.UpdateDate.Value, DateTimeKind.Utc) : null);
}
