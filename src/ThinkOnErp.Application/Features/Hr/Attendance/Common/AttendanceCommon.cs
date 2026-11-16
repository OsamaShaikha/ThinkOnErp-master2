using System;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Common;

internal static class AttendanceCommon
{
    public static BranchAttendanceLocationDto MapBranchLocation(BranchAttendanceLocation value) => new(
        value.BranchId,
        value.CompanyId,
        value.Latitude,
        value.Longitude,
        value.RadiusMeters,
        value.EnforceGeofence,
        BranchAttendanceLocationRules.Version(value),
        value.CreationUser,
        DateTime.SpecifyKind(value.CreationDate, DateTimeKind.Utc),
        value.UpdateUser,
        value.UpdateDate.HasValue ? DateTime.SpecifyKind(value.UpdateDate.Value, DateTimeKind.Utc) : null);

    public static RawAttendanceDto MapRawAttendance(RawAttendance raw) => new(
        raw.Id,
        raw.EmployeeCode,
        new DateTimeOffset(DateTime.SpecifyKind(raw.PunchTime, DateTimeKind.Utc)),
        raw.PunchType,
        raw.DeviceId,
        raw.Source,
        raw.ExternalReference,
        raw.EventId,
        raw.IsProcessed,
        raw.ResolvedWorkDate.HasValue ? DateOnly.FromDateTime(raw.ResolvedWorkDate.Value) : null,
        raw.AttendanceDayId,
        raw.ProcessingOutcome,
        raw.ProcessingReason,
        raw.ProcessedBy,
        raw.ProcessedDate.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(raw.ProcessedDate.Value, DateTimeKind.Utc)) : null,
        raw.LastAttemptedBy,
        raw.LastAttemptedAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(raw.LastAttemptedAtUtc.Value, DateTimeKind.Utc)) : null);

    public static AttendanceCorrectionRequest ForRead(AttendanceCorrectionRequest r)
    {
        static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
        return new AttendanceCorrectionRequest
        {
            Id = r.Id,
            EmployeeCode = r.EmployeeCode,
            AttendanceDate = DateTime.SpecifyKind(r.AttendanceDate.Date, DateTimeKind.Unspecified),
            OldCheckIn = Utc(r.OldCheckIn),
            OldCheckOut = Utc(r.OldCheckOut),
            RequestedCheckIn = Utc(r.RequestedCheckIn),
            RequestedCheckOut = Utc(r.RequestedCheckOut),
            AppliedCheckIn = Utc(r.AppliedCheckIn),
            AppliedCheckOut = Utc(r.AppliedCheckOut),
            OriginalDayVersion = r.OriginalDayVersion,
            Reason = r.Reason,
            Status = r.Status,
            ApprovedBy = r.ApprovedBy,
            ApprovalDate = Utc(r.ApprovalDate),
            RejectionReason = r.RejectionReason,
            CreationUser = r.CreationUser,
            CreationDate = DateTime.SpecifyKind(r.CreationDate, DateTimeKind.Utc),
            UpdateUser = r.UpdateUser,
            UpdateDate = Utc(r.UpdateDate)
        };
    }
}
