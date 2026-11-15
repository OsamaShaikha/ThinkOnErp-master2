namespace ThinkOnErp.Domain.Entities.Hr;

public record AttendanceDayContext(bool IsHoliday, IReadOnlyList<LeaveRequest> ApprovedLeaves, bool HasOnlineEvidence, bool HasPendingEvidence = false);
public record AttendanceStatusDecision(string Status, string? LeaveTypeCode, bool NeedsReview, string? ReviewReason);

public static class AttendanceDayStatusRules
{
    public static AttendanceStatusDecision Decide(string scheduleStatus, bool hasIn, bool hasOut, DateTimeOffset? endUtc,
        DateTimeOffset now, AttendanceDayContext context)
    {
        bool hasPunch = hasIn || hasOut;
        if (context.HasPendingEvidence)
        {
            return new("REVIEW_REQUIRED", null, true, "Unprocessed evidence must be applied or explicitly reviewed before finalizing this day.");
        }

        bool conflict = context.ApprovedLeaves.Count > 0 && hasPunch;
        if (hasPunch)
        {
            return new(conflict ? "REVIEW_REQUIRED" : hasIn && hasOut ? "PRESENT" : "INCOMPLETE", null, conflict,
            conflict ? "Recorded attendance overlaps approved leave; review both records." : null);
        }

        if (context.HasOnlineEvidence)
        {
            return new("REVIEW_REQUIRED", null, true, "Processed online evidence has no daily punches; reconstruct the day before classifying absence.");
        }

        if (context.IsHoliday)
        {
            return new("HOLIDAY", null, false, null);
        }

        if (scheduleStatus is "REST_DAY" or "UNSCHEDULED")
        {
            return new(scheduleStatus, null, false, null);
        }

        if (context.ApprovedLeaves.Count > 1)
        {
            return new("REVIEW_REQUIRED", null, true, "Multiple approved leaves overlap this work date.");
        }

        if (context.ApprovedLeaves.Count == 1)
        {
            LeaveRequest leave = context.ApprovedLeaves[0];
            return leave.DaysRequested < 1m
                ? new("REVIEW_REQUIRED", leave.LeaveTypeCode, true, "Partial-day leave has no timed interval; absence cannot be inferred.")
                : new("ON_LEAVE", leave.LeaveTypeCode, false, null);
        }
        return scheduleStatus != "WORKING" || endUtc == null
            ? throw new InvalidOperationException("Working-day classification requires an explicit scheduled end.")
            : new(now < endUtc ? "PENDING" : "ABSENT", null, false, null);
    }
}
