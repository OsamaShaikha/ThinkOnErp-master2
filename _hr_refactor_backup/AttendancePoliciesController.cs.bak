using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/attendance/policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AttendancePoliciesController(IPolicyRepository policies) : ControllerBase
{
    [HttpGet]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendancePolicy>>>> GetPolicies(CancellationToken ct)
    {
        var tenant = GetTenant();
        var result = await policies.GetAttendancePoliciesAsync(tenant.CompanyId, ct);
        return Ok(ApiResponse<IReadOnlyList<AttendancePolicy>>.CreateSuccess(result));
    }

    [HttpGet("{id:long}")]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> GetPolicy(long id, CancellationToken ct)
    {
        var result = (await policies.GetAttendancePoliciesAsync(GetTenant().CompanyId, ct)).SingleOrDefault(p => p.Id == id);
        return result == null
            ? NotFound(ApiResponse<AttendancePolicy>.CreateFailure("Attendance policy not found.", statusCode: StatusCodes.Status404NotFound))
            : Ok(ApiResponse<AttendancePolicy>.CreateSuccess(result));
    }

    [HttpPost]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> CreatePolicy(
        [FromBody] CreateAttendancePolicyDto request, CancellationToken ct)
    {
        if (!Valid(request, out var error))
            return BadRequest(ApiResponse<AttendancePolicy>.CreateFailure(error!, statusCode: StatusCodes.Status400BadRequest));

        var tenant = GetTenant();
        var existing = await policies.GetAttendancePoliciesAsync(tenant.CompanyId, ct);
        var conflictScope = existing.Where(p =>
            string.Equals(p.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase) || request.IsDefault && p.IsDefault);
        foreach (var prior in conflictScope.Where(p => p.IsActive && !p.EffectiveTo.HasValue && p.EffectiveFrom.Date < request.EffectiveFrom.Date))
        {
            prior.EffectiveTo = request.EffectiveFrom.Date.AddDays(-1);
            prior.UpdateUser = User.Identity?.Name ?? "SYSTEM";
            prior.UpdateDate = DateTime.UtcNow;
        }
        if (conflictScope.Any(p => Overlaps(p.EffectiveFrom, p.EffectiveTo, request.EffectiveFrom, request.EffectiveTo)))
            return Conflict(ApiResponse<AttendancePolicy>.CreateFailure("A policy with the same code or default scope overlaps this effective period.", statusCode: StatusCodes.Status409Conflict));

        var now = DateTime.UtcNow;
        var policy = new AttendancePolicy
        {
            CompanyId = tenant.CompanyId,
            Code = request.Code.Trim().ToUpperInvariant(),
            NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(),
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            GracePeriodMinutes = request.GracePeriodMinutes,
            EarlyLeaveToleranceMinutes = request.EarlyLeaveToleranceMinutes,
            MinMinutesForOvertime = request.MinMinutesForOvertime,
            AutoDeductLateArrival = request.AutoDeductLateArrival,
            MissingPunchHandling = request.MissingPunchHandling.Trim().ToUpperInvariant(),
            AllowMultipleSessions = request.AllowMultipleSessions, IsDefault = request.IsDefault,
            EnforceGeofence = request.EnforceGeofence,
            DefaultAllowedRadiusMeters = request.DefaultAllowedRadiusMeters,
            GeofenceViolationAction = request.GeofenceViolationAction.Trim().ToUpperInvariant(),
            AllowAnyBranchPunch = request.AllowAnyBranchPunch,
            IsActive = true, CreationUser = User.Identity?.Name ?? "SYSTEM", CreationDate = now
        };
        await policies.AddAttendancePolicyAsync(policy, ct);
        await policies.SaveChangesAsync(ct);
        return Ok(ApiResponse<AttendancePolicy>.CreateSuccess(policy, "Attendance policy version created."));
    }

    [HttpPost("{id:long}/deactivate")]
    [HrPermission("hr-attendance", "configure")]
    public async Task<ActionResult<ApiResponse<AttendancePolicy>>> Deactivate(long id, CancellationToken ct)
    {
        var policy = (await policies.GetAttendancePoliciesAsync(GetTenant().CompanyId, ct)).SingleOrDefault(p => p.Id == id);
        if (policy == null)
            return NotFound(ApiResponse<AttendancePolicy>.CreateFailure("Attendance policy not found.", statusCode: StatusCodes.Status404NotFound));
        if (!policy.IsActive)
            return Conflict(ApiResponse<AttendancePolicy>.CreateFailure("Attendance policy is already inactive.", statusCode: StatusCodes.Status409Conflict));
        var cutoff = policy.EffectiveFrom.Date >= DateTime.UtcNow.Date
            ? policy.EffectiveFrom.Date.AddDays(-1)
            : DateTime.UtcNow.Date.AddDays(-1);
        if (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value.Date < cutoff)
            cutoff = policy.EffectiveTo.Value.Date;
        policy.EffectiveTo = cutoff;
        policy.IsActive = false;
        policy.UpdateUser = User.Identity?.Name ?? "SYSTEM";
        policy.UpdateDate = DateTime.UtcNow;
        await policies.SaveChangesAsync(ct);
        return Ok(ApiResponse<AttendancePolicy>.CreateSuccess(policy, "Attendance policy deactivated."));
    }

    private TenantRequestContext GetTenant()
        => HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) && value is TenantRequestContext tenant && tenant.CompanyId > 0
            ? tenant
            : throw new InvalidOperationException("Trusted tenant context is required.");

    private static bool Valid(CreateAttendancePolicyDto request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 50 ||
            string.IsNullOrWhiteSpace(request.NameAr) || request.NameAr.Length > 200 ||
            string.IsNullOrWhiteSpace(request.NameEn) || request.NameEn.Length > 200 ||
            request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            error = "Policy identifiers, names or effective period are invalid.";
        else if (request.GracePeriodMinutes is < 0 or > 1440 || request.EarlyLeaveToleranceMinutes is < 0 or > 1440 ||
                 request.MinMinutesForOvertime is < 0 or > 1440 || request.DefaultAllowedRadiusMeters is < 0 or > 100000)
            error = "Attendance policy thresholds are outside supported ranges.";
        else if (string.IsNullOrWhiteSpace(request.MissingPunchHandling) || string.IsNullOrWhiteSpace(request.GeofenceViolationAction) ||
                 request.MissingPunchHandling.Trim().ToUpperInvariant() is not ("PENALIZE_HALF_DAY" or "PENALIZE_FULL_DAY" or "FLAG_ONLY") ||
                 request.GeofenceViolationAction.Trim().ToUpperInvariant() is not ("REJECT" or "FLAG_AND_ALLOW"))
            error = "Attendance policy action is invalid.";
        return error == null;
    }

    private static bool Overlaps(DateTime aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => aFrom.Date <= (bTo?.Date ?? DateTime.MaxValue.Date) && bFrom.Date <= (aTo?.Date ?? DateTime.MaxValue.Date);
}
