using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class EffectiveShiftTests
{
    private readonly Mock<IEmployeeRepository> employees = new();
    private readonly Mock<IShiftResolutionRepository> repository = new();
    private readonly List<EmployeeShiftAssignment> assignments = new();
    private readonly List<EmployeeShiftDay> days = new();
    private readonly Dictionary<string, ShiftSchedule> catalog = new();
    private readonly DateOnly date = new(2026, 11, 5); // Thursday (4), not the night shift's end weekday.
    private readonly EffectiveShiftService service;

    public EffectiveShiftTests()
    {
        employees.Setup(e => e.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.GetDailyShiftsAsync("E1", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => days.ToArray());
        repository.Setup(r => r.GetAssignmentsAsync("E1", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => assignments.ToArray());
        repository.Setup(r => r.GetShiftAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken ct) => catalog.GetValueOrDefault(code));
        foreach (var code in new[] { "A", "B" }) catalog[code] = new ShiftSchedule { ShiftCode = code, StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17), BreakMinutes = 30, WorkingDaysJson = "[0,1,2,3,4,5,6]" };
        service = new EffectiveShiftService(employees.Object, repository.Object);
    }

    private EmployeeShiftAssignment Base(long id = 1) => new() { Id = id, EmployeeCode = "E1", ShiftCode = "A", EffectiveFrom = new DateTime(2026, 1, 1) };
    private EmployeeShiftAssignment Temporary(long id = 2) => new() { Id = id, EmployeeCode = "E1", ShiftCode = "B",
        EffectiveFrom = new DateTime(2026, 11, 5, 16, 0, 0), EffectiveTo = new DateTime(2026, 11, 7) };

    [Theory]
    [InlineData(4, "BASE", "A")]
    [InlineData(5, "TEMPORARY", "B")]
    [InlineData(7, "TEMPORARY", "B")]
    [InlineData(8, "BASE", "A")]
    public async Task TemporaryOverridesBaseOnBothInclusiveDates(int day, string source, string code)
    {
        assignments.AddRange(new[] { Base(), Temporary() });
        var result = await service.ResolveAsync("E1", new DateOnly(2026, 11, day));
        Assert.Equal(source, result.Source);
        Assert.Equal(code, result.ShiftCode);
        Assert.Equal(7.5m, result.ScheduledHours);
    }

    [Theory]
    [InlineData(4, "BASE")]
    [InlineData(5, "TEMPORARY")]
    [InlineData(7, "TEMPORARY")]
    [InlineData(8, "BASE")]
    public async Task AClosedBaseAssignmentRemainsBaseWhenItsKindIsExplicit(int day, string expected)
    {
        var baseline = Base(); baseline.AssignmentKind = "BASE"; baseline.EffectiveTo = new DateTime(2026, 12, 31);
        var temporary = Temporary(); temporary.AssignmentKind = "TEMPORARY";
        assignments.AddRange(new[] { baseline, temporary });
        Assert.Equal(expected, (await service.ResolveAsync("E1", new DateOnly(2026, 11, day))).Source);
    }

    [Theory]
    [InlineData("TEMPORARY")]
    [InlineData("UNKNOWN")]
    public async Task InvalidExplicitAssignmentKindsAreNotTreatedAsBase(string kind)
    {
        var invalid = Base(); invalid.AssignmentKind = kind; assignments.Add(invalid);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync("E1", date));
    }

    [Theory]
    [InlineData("MANUAL")]
    [InlineData("GENERATED")]
    public async Task DailyRowIsAuthoritativeOverConflictingTemplates(string source)
    {
        assignments.AddRange(new[] { Base(), Temporary(2), Temporary(3) });
        days.Add(new EmployeeShiftDay { Id = 7, EmployeeCode = "E1", WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = "A", Source = source });
        var result = await service.ResolveAsync("E1", date);
        Assert.Equal("DAILY_" + source, result.Source);
        Assert.Equal(7L, result.DailyShiftId);
        Assert.Null(result.AssignmentId);
        repository.Verify(r => r.GetAssignmentsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExplicitRestSuppressesBaseWorkWithoutAClockDefault()
    {
        assignments.Add(Base());
        days.Add(new EmployeeShiftDay { EmployeeCode = "E1", WorkDate = date.ToDateTime(TimeOnly.MinValue), IsRestDay = true });
        var result = await service.ResolveAsync("E1", date);
        Assert.Equal("REST_DAY", result.Status);
        Assert.Equal(0m, result.ScheduledHours);
        Assert.Null(result.StartsAtLocal);
        repository.Verify(r => r.GetShiftAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("DAILY")]
    [InlineData("TEMPORARY")]
    [InlineData("BASE")]
    public async Task MultipleCandidatesAtTheChosenPriorityAreRejected(string priority)
    {
        if (priority == "DAILY")
            for (var i = 0; i < 2; i++) days.Add(new EmployeeShiftDay { EmployeeCode = "E1", WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = "A" });
        else assignments.AddRange(priority == "BASE" ? new[] { Base(1), Base(2) } : new[] { Base(), Temporary(2), Temporary(3) });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync("E1", date));
        Assert.Contains("Conflicting", error.Message);
    }

    [Fact]
    public async Task InactiveOtherEmployeeAndOutOfRangeRowsDoNotOverrideTheBase()
    {
        days.Add(new EmployeeShiftDay { EmployeeCode = "E2", WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = "B" });
        days.Add(new EmployeeShiftDay { EmployeeCode = "E1", WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = "B", IsActive = false });
        var inactive = Temporary(); inactive.IsActive = false;
        var other = Temporary(); other.EmployeeCode = "E2";
        var future = Temporary(); future.EffectiveFrom = new DateTime(2026, 11, 6);
        var expired = Temporary(); expired.EffectiveFrom = new DateTime(2026, 10, 1); expired.EffectiveTo = new DateTime(2026, 11, 4);
        assignments.AddRange(new[] { Base(), inactive, other, future, expired });
        Assert.Equal("BASE", (await service.ResolveAsync("E1", date)).Source);
    }

    [Theory]
    [InlineData(5, "A", "WORKING")]
    [InlineData(6, "B", "WORKING")]
    [InlineData(7, null, "REST_DAY")]
    public async Task ComplementaryBasePatternsUseTheWorkingWeekday(int day, string? code, string status)
    {
        var second = Base(2); second.ShiftCode = "B";
        assignments.AddRange(new[] { Base(), second });
        catalog["A"].WorkingDaysJson = "[4]";
        catalog["B"].WorkingDaysJson = "[5]";
        var result = await service.ResolveAsync("E1", new DateOnly(2026, 11, day));
        Assert.Equal(status, result.Status);
        Assert.Equal(code, result.ShiftCode);
        if (status == "REST_DAY") Assert.Equal(0m, result.ScheduledHours);
    }

    [Fact]
    public async Task NoAssignmentMeansUnscheduledRatherThanAnAssumedEightHourDay()
    {
        var result = await service.ResolveAsync("E1", date);
        Assert.Equal("UNSCHEDULED", result.Status);
        Assert.Equal("NONE", result.Source);
        Assert.Equal(0m, result.ScheduledHours);
        Assert.Null(result.StartsAtLocal);
    }

    [Theory]
    [InlineData(false, "REST_DAY")]
    [InlineData(true, "WORKING")]
    public async Task ExplicitDailyShiftCanOverrideARecurringWeekdayOff(bool explicitDay, string expected)
    {
        catalog["A"].WorkingDaysJson = "[6]";
        assignments.Add(Base());
        if (explicitDay) days.Add(new EmployeeShiftDay { EmployeeCode = "E1", WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = "A" });
        Assert.Equal(expected, (await service.ResolveAsync("E1", date)).Status);
    }

    [Fact]
    public async Task NightShiftUsesItsStartDateAndDeductsTheConfiguredBreak()
    {
        assignments.Add(Base());
        catalog["A"].StartTime = TimeSpan.FromHours(22);
        catalog["A"].EndTime = TimeSpan.FromHours(6);
        catalog["A"].WorkingDaysJson = "[4]";
        var result = await service.ResolveAsync("E1", date);
        Assert.Equal(new DateTime(2026, 11, 5, 22, 0, 0), result.StartsAtLocal);
        Assert.Equal(new DateTime(2026, 11, 6, 6, 0, 0), result.EndsAtLocal);
        Assert.Equal(DateTimeKind.Unspecified, result.StartsAtLocal!.Value.Kind);
        Assert.Equal(450m, result.ScheduledMinutes);
        Assert.Equal(7.5m, result.ScheduledHours);
        Assert.Equal(date, result.WorkDate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidSelectedTemporaryShiftDoesNotSilentlyRestoreTheBase(bool missing)
    {
        assignments.AddRange(new[] { Base(), Temporary() });
        if (missing) catalog.Remove("B"); else catalog["B"].IsActive = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync("E1", date));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[7]")]
    [InlineData("[-1]")]
    [InlineData("[1,1]")]
    public async Task MalformedWeekdaysAreRejected(string json)
    {
        assignments.Add(Base()); catalog["A"].WorkingDaysJson = json;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync("E1", date));
    }

    [Theory]
    [InlineData(9, 9, 0)]
    [InlineData(-1, 17, 0)]
    [InlineData(24, 17, 0)]
    [InlineData(9, 24, 0)]
    [InlineData(9, 17, -1)]
    [InlineData(9, 17, 480)]
    public async Task InvalidHoursAndBreaksAreRejected(int start, int end, int pause)
    {
        assignments.Add(Base());
        catalog["A"].StartTime = TimeSpan.FromHours(start); catalog["A"].EndTime = TimeSpan.FromHours(end); catalog["A"].BreakMinutes = pause;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync("E1", date));
    }

    [Fact]
    public async Task OutOfScopeEmployeeIsRejectedBeforeReadingAnySchedule()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync("E2", date));
        repository.Verify(r => r.GetDailyShiftsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
