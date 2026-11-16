using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Common;

internal static class MobilePunchProcessor
{
    public static async Task<AttendancePunchResultDto> RecordPunchAsync(
        IAttendanceCorrectionRepository attendanceRepo,
        IEmployeeRepository employeeRepo,
        IBranchRepository branchRepo,
        IPolicyRepository policyRepo,
        IGeoLocationService geoService,
        ILogger logger,
        IRawAttendanceProcessingService? processing,
        IAttendanceBranchRepository? branchAccess,
        string employeeCode, decimal latitude, decimal longitude, long? branchId,
        decimal? accuracy, string? deviceId, string? source, string? eventId, string type, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 100 || latitude < -90m || latitude > 90m || longitude < -180m || longitude > 180m ||
            accuracy < 0m || accuracy > 99999.99m || deviceId?.Length > 200 || source?.Length > 100)
            throw new ArgumentException("Valid employee, coordinates, accuracy and source/device field lengths are required.");

        var key = MobilePunchEventKey.Create(eventId, employeeCode, type, latitude, longitude, branchId, accuracy, deviceId, source);
        var employee = await employeeRepo.GetEmployeeByCodeAsync(employeeCode, ct) ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var replay = await TryReplayAsync(attendanceRepo, employeeCode, key, ct);
        if (replay != null) return replay;

        if (!string.IsNullOrWhiteSpace(source) && source.Trim().ToUpperInvariant() != "MOBILE_APP")
            throw new ArgumentException("Mobile punch source must be MOBILE_APP; trusted device/online intake uses its own permission-protected endpoint.");

        var now = DateTime.UtcNow;
        var target = branchId ?? employee.BranchId;

        // Legacy direct service callers can only use the already scoped employee's assigned branch.
        // Production DI supplies the explicit company/branch-scoped repository.
        if (branchAccess == null && target != employee.BranchId)
            throw new UnauthorizedAccessException("Scoped branch access is required for a different punch branch.");

        var branch = target.HasValue
            ? branchAccess != null ? await branchAccess.GetAsync(target.Value, ct) : await branchRepo.GetByIdAsync(target.Value)
            : null;

        if (target.HasValue && branch == null)
            throw new KeyNotFoundException("Punch branch not found in the current HR scope.");

        if (branch != null && employee.BranchId.HasValue && branch.Id != employee.BranchId.Value)
        {
            var assignedBranch = await branchRepo.GetByIdAsync(employee.BranchId.Value);
            if (assignedBranch?.CompanyId != branch.CompanyId)
                throw new UnauthorizedAccessException("The punch branch is outside the employee company.");
        }

        var policy = await ResolvePolicyAsync(policyRepo, branch?.CompanyId, now, ct);
        var geoAction = policy.GeofenceViolationAction?.Trim().ToUpperInvariant();
        GeofenceCheckResult location;
        string? rejected;

        if (branch == null || !branch.IsActive || !branch.CompanyId.HasValue)
        {
            location = new GeofenceCheckResult { Status = "NO_ACTIVE_BRANCH", BranchId = null, Message = "No active authorized branch is configured for this punch." };
            rejected = location.Message;
        }
        else
        {
            location = geoService.CheckProximity(latitude, longitude, branch, branch.GeofenceRadiusMeters > 0m ? null : policy.DefaultAllowedRadiusMeters);
            if (!policy.EnforceGeofence)
            {
                location = new GeofenceCheckResult
                {
                    IsWithinRange = true,
                    BranchId = branch.Id,
                    BranchName = location.BranchName,
                    DistanceMeters = location.DistanceMeters,
                    AllowedRadiusMeters = location.AllowedRadiusMeters,
                    Status = "GEOFENCE_DISABLED",
                    Message = "Company policy disables geofence enforcement."
                };
            }

            rejected = geoAction is not ("REJECT" or "FLAG_AND_ALLOW")
                ? "Geofence policy action is invalid; review company settings."
                : !policy.AllowAnyBranchPunch && employee.BranchId != branch.Id
                    ? "Attendance policy does not allow a different branch."
                    : policy.EnforceGeofence && branch.EnforceGeofence && !location.IsWithinRange && geoAction == "REJECT"
                        ? string.IsNullOrWhiteSpace(location.Message) ? "Punch rejected by the configured geofence policy." : location.Message
                        : null;
        }

        var raw = new RawAttendance
        {
            EmployeeCode = employeeCode,
            PunchTime = now,
            PunchType = type,
            DeviceId = deviceId,
            Source = string.IsNullOrWhiteSpace(source) ? "MOBILE_APP" : source.Trim().ToUpperInvariant(),
            EventId = key.Id,
            EventPayloadHash = key.PayloadHash,
            BranchId = branch?.IsActive == true ? branch.Id : null,
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracy,
            DistanceToBranchMeters = location.DistanceMeters,
            IsWithinGeofence = location.Status == "GEOFENCE_DISABLED" ? null : location.IsWithinRange,
            GeofenceStatus = location.Status,
            IsProcessed = rejected != null,
            ProcessingOutcome = rejected == null ? null : "REJECTED",
            ProcessingReason = rejected == null ? null : rejected.Length <= 1000 ? rejected : rejected[..1000],
            ProcessedBy = rejected == null ? null : user,
            ProcessedDate = rejected == null ? null : now,
            CreationUser = user,
            CreationDate = now
        };

        string Receipt(long id) => JsonSerializer.Serialize(new AttendancePunchResultDto(
            rejected == null,
            employeeCode,
            type,
            now,
            raw.BranchId,
            branch?.BranchNameEn ?? branch?.BranchNameLocal ?? string.Empty,
            latitude,
            longitude,
            location.DistanceMeters,
            location.AllowedRadiusMeters,
            location.IsWithinRange,
            location.Status,
            rejected ?? "Attendance evidence saved. Check day status for its financial processing outcome.",
            id,
            rejected == null));

        var stored = await attendanceRepo.AcceptMobileEvidenceAsync(raw, Receipt, ct);
        var result = JsonSerializer.Deserialize<AttendancePunchResultDto>(stored.EventResponseJson ?? throw new InvalidOperationException("Evidence receipt was not saved."))
            ?? throw new InvalidOperationException("Evidence receipt could not be read.");

        if (raw.Id > 0 && stored.Id == raw.Id && !stored.IsProcessed && processing != null)
        {
            try
            {
                await processing.ProcessAsync(stored.Id, user, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Attendance evidence {RawId} was saved; financial processing deferred ({ErrorType}).", stored.Id, ex.GetType().Name);
                var reason = ex is InvalidOperationException or ArgumentException or KeyNotFoundException ? ex.Message : "Financial processing failed; review the evidence queue.";
                try
                {
                    await attendanceRepo.RecordRawAttemptAsync(stored.Id, user, reason, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception recordingError)
                {
                    logger.LogWarning("Evidence {RawId} is durable but its attempt note could not be saved ({ErrorType}).", stored.Id, recordingError.GetType().Name);
                }
            }
        }

        return result;
    }

    private static async Task<AttendancePunchResultDto?> TryReplayAsync(IAttendanceCorrectionRepository attendanceRepo, string employeeCode, MobilePunchEventKey key, CancellationToken ct)
    {
        var raw = await attendanceRepo.GetMobileEventAsync(employeeCode, key.Id, ct);
        if (raw == null) return null;
        if (raw.EventPayloadHash != key.PayloadHash) throw new MobilePunchEventConflictException();
        if (string.IsNullOrEmpty(raw.EventResponseJson)) throw new InvalidOperationException("Accepted event response is missing; review the stored evidence.");
        return JsonSerializer.Deserialize<AttendancePunchResultDto>(raw.EventResponseJson)
            ?? throw new InvalidOperationException("Accepted event response could not be read.");
    }

    private static async Task<AttendancePolicy> ResolvePolicyAsync(IPolicyRepository policyRepo, long? companyId, DateTime date, CancellationToken cancellationToken)
    {
        if (companyId.HasValue)
        {
            var policy = await policyRepo.GetActiveAttendancePolicyAsync(companyId.Value, date, cancellationToken);
            if (policy != null) return policy;
        }

        return new AttendancePolicy
        {
            EnforceGeofence = true,
            DefaultAllowedRadiusMeters = 100,
            GeofenceViolationAction = "REJECT",
            AllowAnyBranchPunch = false
        };
    }
}
