using System;
using System.Collections.Generic;
using System.Linq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class AttendanceMultipleSessionsTests
{
    private static RawAttendance Punch(long id, int hour, int minute, string type)
    {
        return new RawAttendance
        {
            Id = id,
            EmployeeCode = "EMP_MULTI",
            PunchTime = new DateTime(2026, 11, 8, hour, minute, 0, DateTimeKind.Utc),
            PunchType = type,
            DeviceId = "DEV_01",
            Source = "BIOMETRIC",
            EventId = $"EVENT_{id}",
            EventPayloadHash = $"HASH_{id}"
        };
    }

    [Fact]
    public void MultipleSessions_WhenNotAllowed_RequiresReview_ByDefaultForBackwardCompatibility()
    {
        // Session 1: 08:00 - 12:00, Session 2: 13:00 - 17:00
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 12, 0, "OUT"),
            Punch(3, 13, 0, "IN"),
            Punch(4, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: false);

        Assert.True(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
    }

    [Fact]
    public void MultipleSessions_WhenAllowed_AggregatesSessionMinutesCorrectly()
    {
        // Session 1: 08:00 - 12:00 (240 min = 4.0 hrs)
        // Session 2: 13:00 - 17:00 (240 min = 4.0 hrs)
        // Total = 480 min (8.0 hrs)
        // Break gap = 12:00 - 13:00 = 60 min
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 12, 0, "OUT"),
            Punch(3, 13, 0, "IN"),
            Punch(4, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Equal(480m, summary.TotalSessionMinutes);
        Assert.Equal(60m, summary.TotalBreakGapMinutes);
        Assert.False(summary.IsCurrentlyInside);
        Assert.Equal(punches[0].PunchTime, summary.FirstIn);
        Assert.Equal(punches[3].PunchTime, summary.LastOut);
        Assert.Equal(2, summary.Sessions!.Count);
        Assert.Equal(240m, summary.Sessions[0].DurationMinutes);
        Assert.Equal(240m, summary.Sessions[1].DurationMinutes);
    }

    [Fact]
    public void MultipleSessions_TracksPresenceState_WhenEmployeeIsCurrentlyInside()
    {
        // Session 1: 08:00 - 12:00
        // Session 2: 13:00 - (currently in)
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 12, 0, "OUT"),
            Punch(3, 13, 0, "IN")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.True(summary.IsCurrentlyInside);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Null(summary.Sessions![1].CheckOut);
    }

    [Fact]
    public void MultipleSessions_PreventsDoubleBreakDeduction_InScheduledAttendanceCalculator()
    {
        var date = new DateOnly(2026, 11, 8);
        var schedule = ScheduledAttendanceTests.Schedule(date, 8, 17); // 08:00 to 17:00, with 30 min break
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, AllowMultipleSessions = true };

        // 2 sessions: 08:00 - 12:00 (240 min) and 13:00 - 17:00 (240 min)
        // Total session net minutes = 480 minutes (8.0 hours)
        // The 60-minute gap from 12:00 to 13:00 is greater than the scheduled break of 30 min.
        // Therefore, net minutes passed to calculator should be 480 minutes, without subtracting break again.
        var result = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime,
            schedule.EndUtc!.Value.UtcDateTime,
            schedule, policy,
            multiSessionNetMinutes: 480m
        );

        Assert.Equal(8.0m, result.ActualWorkedHours);
        Assert.Equal(0, result.LateArrivalMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
    }

    [Fact]
    public void MultipleSessions_FlagsReview_WhenPunchesAreReversedOrTied()
    {
        // OUT at 08:00 before IN at 09:00
        var reversedPunches = new[]
        {
            Punch(1, 8, 0, "OUT"),
            Punch(2, 9, 0, "IN")
        };

        var summary = AttendanceEvidenceSummary.Build(reversedPunches, null, allowMultipleSessions: true);
        Assert.True(summary.NeedsReview);

        // Tied opposite punches at the exact same second
        var tiedPunches = new[]
        {
            Punch(1, 9, 0, "IN"),
            Punch(2, 9, 0, "OUT")
        };

        var tiedSummary = AttendanceEvidenceSummary.Build(tiedPunches, null, allowMultipleSessions: true);
        Assert.True(tiedSummary.NeedsReview);
    }

    [Fact]
    public void Build_EmptyEvents_ReturnsEmptySummaryWithoutReview()
    {
        var summary = AttendanceEvidenceSummary.Build(Array.Empty<RawAttendance>(), null, allowMultipleSessions: true);

        Assert.Null(summary.FirstIn);
        Assert.Null(summary.LastOut);
        Assert.False(summary.NeedsReview);
        Assert.False(summary.IsCurrentlyInside);
        Assert.Equal(0, summary.TotalSessionsCount);
        Assert.Equal(0m, summary.TotalSessionMinutes);
        Assert.Equal(0m, summary.TotalBreakGapMinutes);
        Assert.Empty(summary.Sessions!);
    }

    [Fact]
    public void Build_UnknownPunchType_RequiresReview()
    {
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 12, 0, "INVALID_PUNCH"),
            Punch(3, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.True(summary.NeedsReview);
    }

    [Fact]
    public void Build_MultipleConsecutiveIns_TakesFirstInAndIgnoresSubsequentInSameSession()
    {
        // Two INs: 08:00 and 08:30, then OUT at 17:00
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 8, 30, "IN"),
            Punch(3, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(1, summary.TotalSessionsCount);
        Assert.Equal(punches[0].PunchTime, summary.FirstIn);
        Assert.Equal(punches[2].PunchTime, summary.LastOut);
        // Session duration from 08:00 to 17:00 = 9 hours = 540 minutes
        Assert.Equal(540m, summary.TotalSessionMinutes);
    }

    [Fact]
    public void Build_MultipleConsecutiveOuts_CreatesZeroDurationSessionForOrphanOut()
    {
        // IN at 08:00, OUT at 12:00, then accidental second OUT at 12:05
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 12, 0, "OUT"),
            Punch(3, 12, 5, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        // First session: 08:00 to 12:00 = 240 min
        Assert.Equal(240m, summary.Sessions![0].DurationMinutes);
        // Second orphan OUT session: 12:05 to 12:05 = 0 min
        Assert.Equal(0m, summary.Sessions[1].DurationMinutes);
        Assert.Equal(240m, summary.TotalSessionMinutes);
        Assert.Equal(punches[2].PunchTime, summary.LastOut);
    }

    [Fact]
    public void Build_OrphanOutBeforeIn_CreatesZeroDurationSessionAndTracksNextIn()
    {
        // OUT at 07:45 before morning IN at 08:00, then OUT at 17:00
        var punches = new[]
        {
            Punch(1, 7, 45, "OUT"),
            Punch(2, 8, 0, "IN"),
            Punch(3, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        // First OUT is at 07:45, but LastOut is 17:00 > FirstIn (08:00), so no reversed review flag
        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Equal(0m, summary.Sessions![0].DurationMinutes);
        Assert.Equal(540m, summary.Sessions[1].DurationMinutes);
        Assert.Equal(540m, summary.TotalSessionMinutes);
    }

    [Fact]
    public void Build_PunchesSpanningMidnight_ComputesDurationAcrossDateBoundary()
    {
        // Overnight shift: IN at 2026-11-08 22:00, OUT at 2026-11-09 06:00 (8 hours = 480 minutes)
        var punches = new[]
        {
            new RawAttendance
            {
                Id = 1,
                EmployeeCode = "NIGHT_EMP",
                PunchTime = new DateTime(2026, 11, 8, 22, 0, 0, DateTimeKind.Utc),
                PunchType = "IN"
            },
            new RawAttendance
            {
                Id = 2,
                EmployeeCode = "NIGHT_EMP",
                PunchTime = new DateTime(2026, 11, 9, 6, 0, 0, DateTimeKind.Utc),
                PunchType = "OUT"
            }
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(1, summary.TotalSessionsCount);
        Assert.Equal(480m, summary.TotalSessionMinutes);
        Assert.Equal(punches[0].PunchTime, summary.FirstIn);
        Assert.Equal(punches[1].PunchTime, summary.LastOut);
    }

    [Fact]
    public void Build_WithExistingAttendanceDay_MergesMissingPunchesAndIgnoresExisting()
    {
        var existingDay = new AttendanceDay
        {
            EmployeeCode = "EMP_MULTI",
            AttendanceDate = new DateTime(2026, 11, 8),
            FirstCheckIn = new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc),
            LastCheckOut = new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc)
        };

        // Events only contain the second session: 18:00 IN and 21:00 OUT
        var punches = new[]
        {
            Punch(1, 18, 0, "IN"),
            Punch(2, 21, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, existingDay, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        // Should merge 08:00 IN and 17:00 OUT from existingDay
        Assert.Equal(new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc), summary.FirstIn);
        Assert.Equal(new DateTime(2026, 11, 8, 21, 0, 0, DateTimeKind.Utc), summary.LastOut);
        Assert.Equal(2, summary.TotalSessionsCount);
        // Session 1: 08:00 to 17:00 (540 min), Session 2: 18:00 to 21:00 (180 min)
        Assert.Equal(720m, summary.TotalSessionMinutes);
        // Break gap between 17:00 and 18:00 = 60 min
        Assert.Equal(60m, summary.TotalBreakGapMinutes);
    }

    [Fact]
    public void Build_ThreeDistinctSessions_CalculatesTotalMinutesAndCumulativeBreakGaps()
    {
        // Session 1: 08:00 - 10:00 (120 min)
        // Gap 1: 10:00 - 10:30 (30 min)
        // Session 2: 10:30 - 14:00 (210 min)
        // Gap 2: 14:00 - 15:00 (60 min)
        // Session 3: 15:00 - 18:00 (180 min)
        // Total session = 120 + 210 + 180 = 510 minutes
        // Total gap = 30 + 60 = 90 minutes
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 10, 0, "OUT"),
            Punch(3, 10, 30, "IN"),
            Punch(4, 14, 0, "OUT"),
            Punch(5, 15, 0, "IN"),
            Punch(6, 18, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(3, summary.TotalSessionsCount);
        Assert.Equal(510m, summary.TotalSessionMinutes);
        Assert.Equal(90m, summary.TotalBreakGapMinutes);
        Assert.False(summary.IsCurrentlyInside);
    }

    [Fact]
    public void Build_OnlyInPunch_ResultsInOpenSessionAndIsInsideTrue()
    {
        var punches = new[]
        {
            Punch(1, 8, 0, "IN")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.True(summary.IsCurrentlyInside);
        Assert.Equal(1, summary.TotalSessionsCount);
        Assert.Equal(punches[0].PunchTime, summary.FirstIn);
        Assert.Null(summary.LastOut);
        Assert.Equal(0m, summary.TotalSessionMinutes);
        Assert.Null(summary.Sessions![0].CheckOut);
    }

    [Fact]
    public void Build_UnsortedPunches_AreSortedChronologicallyBeforeProcessing()
    {
        // Provide events in reverse / mixed order
        var punches = new[]
        {
            Punch(4, 17, 0, "OUT"),
            Punch(1, 8, 0, "IN"),
            Punch(3, 13, 0, "IN"),
            Punch(2, 12, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Equal(480m, summary.TotalSessionMinutes);
        Assert.Equal(60m, summary.TotalBreakGapMinutes);
        Assert.Equal(punches[1].PunchTime, summary.FirstIn); // 08:00
        Assert.Equal(punches[0].PunchTime, summary.LastOut); // 17:00
        Assert.Equal(240m, summary.Sessions![0].DurationMinutes); // 08:00-12:00
        Assert.Equal(240m, summary.Sessions[1].DurationMinutes); // 13:00-17:00
    }

    [Fact]
    public void Build_DuplicatePunchesAtSameTick_WithSameType_HandledWithoutFlaggingReview()
    {
        // Two identical IN punches from network replay/duplicate biometric push
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 8, 0, "IN"),
            Punch(3, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Single(summary.Sessions!);
        Assert.Equal(540m, summary.TotalSessionMinutes);
    }

    [Fact]
    public void Build_OnlyOutPunches_CreatesZeroDurationOrphanSessions()
    {
        var punches = new[]
        {
            Punch(1, 10, 0, "OUT"),
            Punch(2, 14, 0, "OUT"),
            Punch(3, 18, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Null(summary.FirstIn);
        Assert.Equal(punches[2].PunchTime, summary.LastOut);
        Assert.Equal(3, summary.TotalSessionsCount);
        Assert.Equal(0m, summary.TotalSessionMinutes);
        Assert.All(summary.Sessions!, s => Assert.Equal(0m, s.DurationMinutes));
    }

    [Fact]
    public void Build_WhenAllowMultipleSessionsFalse_AndOnlyOneSession_DoesNotRequireReview()
    {
        var punches = new[]
        {
            Punch(1, 8, 30, "IN"),
            Punch(2, 17, 30, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: false);

        Assert.False(summary.NeedsReview);
        Assert.Equal(1, summary.TotalSessionsCount);
        Assert.Equal(540m, summary.TotalSessionMinutes);
    }

    [Fact]
    public void Build_FourSessionsWithVaryingGaps_CalculatesExactCumulativeBreakGap()
    {
        // S1: 08:00 - 10:00 (120m)
        // Gap1: 10:00 - 10:15 (15m)
        // S2: 10:15 - 12:30 (135m)
        // Gap2: 12:30 - 13:30 (60m)
        // S3: 13:30 - 16:00 (150m)
        // Gap3: 16:00 - 16:10 (10m)
        // S4: 16:10 - 18:00 (110m)
        // Total session = 120 + 135 + 150 + 110 = 515 min
        // Total break gap = 15 + 60 + 10 = 85 min
        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 10, 0, "OUT"),
            Punch(3, 10, 15, "IN"),
            Punch(4, 12, 30, "OUT"),
            Punch(5, 13, 30, "IN"),
            Punch(6, 16, 0, "OUT"),
            Punch(7, 16, 10, "IN"),
            Punch(8, 18, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(4, summary.TotalSessionsCount);
        Assert.Equal(515m, summary.TotalSessionMinutes);
        Assert.Equal(85m, summary.TotalBreakGapMinutes);
    }

    [Fact]
    public void Build_OvernightShift_WithMultipleNightSessions_CalculatesAccurately()
    {
        // Night 1: 22:00 to 02:00 (240m)
        // Gap: 02:00 to 02:30 (30m)
        // Night 2: 02:30 to 06:00 (210m)
        // Total = 450m (7.5 hrs)
        var punches = new[]
        {
            new RawAttendance { Id = 1, EmployeeCode = "N", PunchTime = new DateTime(2026, 11, 8, 22, 0, 0, DateTimeKind.Utc), PunchType = "IN" },
            new RawAttendance { Id = 2, EmployeeCode = "N", PunchTime = new DateTime(2026, 11, 9, 2, 0, 0, DateTimeKind.Utc), PunchType = "OUT" },
            new RawAttendance { Id = 3, EmployeeCode = "N", PunchTime = new DateTime(2026, 11, 9, 2, 30, 0, DateTimeKind.Utc), PunchType = "IN" },
            new RawAttendance { Id = 4, EmployeeCode = "N", PunchTime = new DateTime(2026, 11, 9, 6, 0, 0, DateTimeKind.Utc), PunchType = "OUT" }
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Equal(450m, summary.TotalSessionMinutes);
        Assert.Equal(30m, summary.TotalBreakGapMinutes);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_WithMultiSessionNetMinutes_CalculatesOvertimeAccurately()
    {
        var date = new DateOnly(2026, 11, 8);
        var schedule = ScheduledAttendanceTests.Schedule(date, 8, 17); // 8 scheduled hours (9 elapsed - 1 break)
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, AllowMultipleSessions = true, MinMinutesForOvertime = 30 };

        // 600 net minutes = 10.0 hours worked -> 2.0 hours overtime
        var result = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime,
            schedule.EndUtc!.Value.UtcDateTime,
            schedule, policy,
            multiSessionNetMinutes: 600m
        );

        Assert.Equal(10.0m, result.ActualWorkedHours);
        Assert.Equal(1.5m, result.OvertimeHours);
        Assert.Equal(0, result.LateArrivalMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
        Assert.Equal("PRESENT", result.Status);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_WithMultiSessionNetMinutes_BelowScheduledHours_SetsDeficitWithoutOvertime()
    {
        var date = new DateOnly(2026, 11, 8);
        var schedule = ScheduledAttendanceTests.Schedule(date, 8, 17); // 8 scheduled hours
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, AllowMultipleSessions = true };

        // 360 net minutes = 6.0 hours worked -> 0 overtime
        var result = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime,
            schedule.EndUtc!.Value.UtcDateTime,
            schedule, policy,
            multiSessionNetMinutes: 360m
        );

        Assert.Equal(6.0m, result.ActualWorkedHours);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal("PRESENT", result.Status);
    }

    [Fact]
    public void Build_ExistingDayWithFirstInAndLastOut_DoesNotDuplicateIfAlreadyPresentInEvents()
    {
        var existingDay = new AttendanceDay
        {
            EmployeeCode = "EMP_MULTI",
            AttendanceDate = new DateTime(2026, 11, 8),
            FirstCheckIn = new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc),
            LastCheckOut = new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc)
        };

        var punches = new[]
        {
            Punch(1, 8, 0, "IN"),
            Punch(2, 17, 0, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, existingDay, allowMultipleSessions: true);

        Assert.Single(summary.Sessions!);
        Assert.Equal(540m, summary.TotalSessionMinutes);
    }
}

