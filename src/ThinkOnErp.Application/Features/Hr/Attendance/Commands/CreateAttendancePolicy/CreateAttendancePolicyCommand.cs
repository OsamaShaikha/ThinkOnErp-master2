using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.CreateAttendancePolicy;

public sealed record CreateAttendancePolicyCommand(CreateAttendancePolicyDto Dto, long CompanyId, string CurrentUser)
    : IRequest<ApiResponse<AttendancePolicy>>;

public sealed class CreateAttendancePolicyCommandValidator : AbstractValidator<CreateAttendancePolicyCommand>
{
    public CreateAttendancePolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.CompanyId).GreaterThan(0);
    }
}

public sealed class CreateAttendancePolicyCommandHandler
    : IRequestHandler<CreateAttendancePolicyCommand, ApiResponse<AttendancePolicy>>
{
    private readonly IPolicyRepository _policies;

    public CreateAttendancePolicyCommandHandler(IPolicyRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<AttendancePolicy>> Handle(CreateAttendancePolicyCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (!Valid(dto, out var error))
        {
            return ApiResponse<AttendancePolicy>.CreateFailure(error!, statusCode: (int)HttpStatusCode.BadRequest);
        }

        var existing = await _policies.GetAttendancePoliciesAsync(request.CompanyId, cancellationToken);
        var conflictScope = existing.Where(p =>
            string.Equals(p.Code, dto.Code.Trim(), StringComparison.OrdinalIgnoreCase) || (dto.IsDefault && p.IsDefault)).ToList();

        foreach (var prior in conflictScope.Where(p => p.IsActive && !p.EffectiveTo.HasValue && p.EffectiveFrom.Date < dto.EffectiveFrom.Date))
        {
            prior.EffectiveTo = dto.EffectiveFrom.Date.AddDays(-1);
            prior.UpdateUser = request.CurrentUser;
            prior.UpdateDate = DateTime.UtcNow;
        }

        if (conflictScope.Any(p => Overlaps(p.EffectiveFrom, p.EffectiveTo, dto.EffectiveFrom, dto.EffectiveTo)))
        {
            return ApiResponse<AttendancePolicy>.CreateFailure(
                "A policy with the same code or default scope overlaps this effective period.",
                statusCode: (int)HttpStatusCode.Conflict);
        }

        var now = DateTime.UtcNow;
        var policy = new AttendancePolicy
        {
            CompanyId = request.CompanyId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            GracePeriodMinutes = dto.GracePeriodMinutes,
            EarlyLeaveToleranceMinutes = dto.EarlyLeaveToleranceMinutes,
            MinMinutesForOvertime = dto.MinMinutesForOvertime,
            AutoDeductLateArrival = dto.AutoDeductLateArrival,
            MissingPunchHandling = dto.MissingPunchHandling.Trim().ToUpperInvariant(),
            AllowMultipleSessions = dto.AllowMultipleSessions,
            IsDefault = dto.IsDefault,
            EnforceGeofence = dto.EnforceGeofence,
            DefaultAllowedRadiusMeters = dto.DefaultAllowedRadiusMeters,
            GeofenceViolationAction = dto.GeofenceViolationAction.Trim().ToUpperInvariant(),
            AllowAnyBranchPunch = dto.AllowAnyBranchPunch,
            IsActive = true,
            CreationUser = request.CurrentUser,
            CreationDate = now
        };

        await _policies.AddAttendancePolicyAsync(policy, cancellationToken);
        await _policies.SaveChangesAsync(cancellationToken);

        return ApiResponse<AttendancePolicy>.CreateSuccess(policy, "Attendance policy version created.");
    }

    private static bool Valid(CreateAttendancePolicyDto request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 50 ||
            string.IsNullOrWhiteSpace(request.NameAr) || request.NameAr.Length > 200 ||
            string.IsNullOrWhiteSpace(request.NameEn) || request.NameEn.Length > 200 ||
            request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
        {
            error = "Policy identifiers, names or effective period are invalid.";
        }
        else if (request.GracePeriodMinutes is < 0 or > 1440 || request.EarlyLeaveToleranceMinutes is < 0 or > 1440 ||
                 request.MinMinutesForOvertime is < 0 or > 1440 || request.DefaultAllowedRadiusMeters is < 0 or > 100000)
        {
            error = "Attendance policy thresholds are outside supported ranges.";
        }
        else if (string.IsNullOrWhiteSpace(request.MissingPunchHandling) || string.IsNullOrWhiteSpace(request.GeofenceViolationAction) ||
                 request.MissingPunchHandling.Trim().ToUpperInvariant() is not ("PENALIZE_HALF_DAY" or "PENALIZE_FULL_DAY" or "FLAG_ONLY") ||
                 request.GeofenceViolationAction.Trim().ToUpperInvariant() is not ("REJECT" or "FLAG_AND_ALLOW"))
        {
            error = "Attendance policy action is invalid.";
        }
        return error == null;
    }

    private static bool Overlaps(DateTime aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => aFrom.Date <= (bTo?.Date ?? DateTime.MaxValue.Date) && bFrom.Date <= (aTo?.Date ?? DateTime.MaxValue.Date);
}
