using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShiftGenerationTests
{
    private static readonly DateOnly Start = new(2026, 11, 2); // Monday
    private static readonly Dictionary<string, ShiftSchedule> Definitions = new()
    {
        ["A"] = new() { ShiftCode = "A", IsActive = true, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), WorkingDaysJson = "[1,2,3,4,5]" },
        ["B"] = new() { ShiftCode = "B", IsActive = true, StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), WorkingDaysJson = "[0,1,2,3,4,5,6]" }
    };
    private static EmployeeShiftAssignment Assignment(string code, string kind = "BASE") => new() { EmployeeCode = "E", ShiftCode = code, AssignmentKind = kind,
        EffectiveFrom = Start.ToDateTime(TimeOnly.MinValue), EffectiveTo = Start.AddDays(6).ToDateTime(TimeOnly.MinValue) };
    private static EmployeeShiftDay Day(int offset, string source, bool active = true) => new() { Id = offset + 1, EmployeeCode = "E",
        WorkDate = Start.AddDays(offset).ToDateTime(TimeOnly.MinValue), ShiftCode = "A", Source = source, IsActive = active };

    [Fact]
    public void GeneratesWorkingAndRestDaysAndRepeatedPlanWritesNothing()
    {
        var first = ShiftGenerationService.Plan("E", Start, Start.AddDays(6), Array.Empty<EmployeeShiftDay>(), new[] { Assignment("A") }, Definitions);
        Assert.Equal(7, first.Result.Created); Assert.Equal(2, first.Writes.Count(d => d.IsRestDay && d.ShiftCode == null));
        foreach (var day in first.Writes) day.Id = day.WorkDate.Day;
        var repeat = ShiftGenerationService.Plan("E", Start, Start.AddDays(6), first.Writes, new[] { Assignment("A") }, Definitions);
        Assert.Empty(repeat.Writes); Assert.Equal(7, repeat.Result.Unchanged);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void ManualDaysAreAlwaysPreservedEvenWhenAssignmentsConflict(bool active)
    {
        var result = ShiftGenerationService.Plan("E", Start, Start, new[] { Day(0, "MANUAL", active) },
            new[] { Assignment("A", "TEMPORARY"), Assignment("B", "TEMPORARY") }, Definitions);
        Assert.Empty(result.Writes); Assert.Equal(1, result.Result.PreservedManual);
    }

    [Fact]
    public void TemporaryOverridesBaseAndOnlyGeneratedDaysAreUpdated()
    {
        var result = ShiftGenerationService.Plan("E", Start, Start, new[] { Day(0, "GENERATED") },
            new[] { Assignment("A"), Assignment("B", "TEMPORARY") }, Definitions);
        Assert.Equal("B", Assert.Single(result.Writes).ShiftCode); Assert.Equal(1, result.Result.Updated);
    }

    [Fact]
    public void UnscheduledDoesNotInventRestAndDeactivatesObsoleteGeneratedDay()
    {
        var result = ShiftGenerationService.Plan("E", Start, Start.AddDays(1), new[] { Day(0, "GENERATED") }, Array.Empty<EmployeeShiftAssignment>(), Definitions);
        Assert.Equal(2, result.Result.Unscheduled); Assert.False(Assert.Single(result.Writes).IsActive);
    }

    [Fact]
    public void InactiveGeneratedDayCanBeReactivated()
    {
        var result = ShiftGenerationService.Plan("E", Start, Start, new[] { Day(0, "GENERATED", false) }, new[] { Assignment("A") }, Definitions);
        Assert.True(Assert.Single(result.Writes).IsActive); Assert.Equal(1, result.Result.Updated);
    }

    [Fact]
    public void LateConflictRejectsEntirePlanWithoutMutatingInput()
    {
        var first = Assignment("A"); var conflict = Assignment("B"); conflict.EffectiveFrom = Start.AddDays(2).ToDateTime(TimeOnly.MinValue);
        var old = Day(0, "GENERATED", false);
        Assert.Throws<InvalidOperationException>(() => ShiftGenerationService.Plan("E", Start, Start.AddDays(3), new[] { old }, new[] { first, conflict }, Definitions));
        Assert.False(old.IsActive); Assert.Equal("A", old.ShiftCode);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task InvalidRangesNeverReachDatabase(int scenario)
    {
        var dto = scenario switch { 0 => new GenerateShiftDaysDto(null, Start), 1 => new(Start, null), 2 => new(Start, Start.AddDays(-1)), _ => new(Start, Start.AddDays(366)) };
        var repository = new Mock<IShiftGenerationRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new ShiftGenerationService(repository.Object).GenerateAsync("E", dto, "U"));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void Plan_WhenExistingDayHasInvalidSource_ThrowsInvalidOperationException()
    {
        var corruptedDay = new EmployeeShiftDay
        {
            Id = 99,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = "A",
            Source = "EXTERNAL_INVALID",
            IsActive = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ShiftGenerationService.Plan("E", Start, Start, new[] { corruptedDay }, new[] { Assignment("A") }, Definitions));
        Assert.Contains("Invalid daily shift source", ex.Message);
    }

    [Fact]
    public void Plan_WhenUnscheduledWithExistingActiveGeneratedDay_DeactivatesIt()
    {
        var existing = Day(0, "GENERATED", active: true);
        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { existing }, Array.Empty<EmployeeShiftAssignment>(), Definitions);

        Assert.Equal(1, result.Unscheduled);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Created);
        var write = Assert.Single(writes);
        Assert.Equal(existing.Id, write.Id);
        Assert.False(write.IsActive);
    }

    [Fact]
    public void Plan_WhenUnscheduledWithNoExistingDay_RecordsUnscheduledWithoutWriting()
    {
        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), Array.Empty<EmployeeShiftAssignment>(), Definitions);

        Assert.Equal(1, result.Unscheduled);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenExistingDayAlreadyMatchesWorkingShift_MarksUnchangedWithoutWriting()
    {
        var existing = new EmployeeShiftDay
        {
            Id = 10,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = "A",
            IsRestDay = false,
            IsActive = true,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { existing }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(1, result.Unchanged);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenExistingDayAlreadyMatchesRestDay_MarksUnchangedWithoutWriting()
    {
        // Sunday (offset 6 from Monday Start)
        var sunday = Start.AddDays(6);
        var existingRest = new EmployeeShiftDay
        {
            Id = 11,
            EmployeeCode = "E",
            WorkDate = sunday.ToDateTime(TimeOnly.MinValue),
            ShiftCode = null,
            IsRestDay = true,
            IsActive = true,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", sunday, sunday, new[] { existingRest }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(1, result.Unchanged);
        Assert.Equal(0, result.Created);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenManualRestDayExists_PreservesManualWithoutOverridingWithWorkingShift()
    {
        // Monday Start would normally be working day for Shift A.
        // But there is a MANUAL rest day entry.
        var manualRest = new EmployeeShiftDay
        {
            Id = 12,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = null,
            IsRestDay = true,
            IsActive = true,
            Source = "MANUAL"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { manualRest }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(1, result.PreservedManual);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenFullWeekGenerated_ProducesWorkingDaysAndRestDaysAccordingToShiftWorkingDays()
    {
        // Start is Mon Nov 2, 2026.
        // Mon..Fri = working days (Shift A, IsRestDay=false).
        // Sat..Sun = rest days (ShiftCode=null, IsRestDay=true).
        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start.AddDays(6), Array.Empty<EmployeeShiftDay>(), new[] { Assignment("A") }, Definitions);

        Assert.Equal(7, result.Created);
        Assert.Equal(7, writes.Count);

        var workingDays = writes.Where(w => !w.IsRestDay).ToList();
        var restDays = writes.Where(w => w.IsRestDay).ToList();

        Assert.Equal(5, workingDays.Count);
        Assert.All(workingDays, w => Assert.Equal("A", w.ShiftCode));
        Assert.Equal(2, restDays.Count);
        Assert.All(restDays, r => Assert.Null(r.ShiftCode));
    }

    [Fact]
    public void Plan_AcrossMonthBoundary_GeneratesContiguousSequence()
    {
        var oct28 = new DateOnly(2026, 10, 28);
        var nov3 = new DateOnly(2026, 11, 3);
        var assignment = new EmployeeShiftAssignment
        {
            EmployeeCode = "E",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = oct28.ToDateTime(TimeOnly.MinValue),
            EffectiveTo = nov3.ToDateTime(TimeOnly.MinValue)
        };

        var (result, writes) = ShiftGenerationService.Plan("E", oct28, nov3, Array.Empty<EmployeeShiftDay>(), new[] { assignment }, Definitions);

        Assert.Equal(7, result.Created);
        Assert.Equal(7, writes.Count);
        Assert.Equal(oct28.ToDateTime(TimeOnly.MinValue), writes.First().WorkDate);
        Assert.Equal(nov3.ToDateTime(TimeOnly.MinValue), writes.Last().WorkDate);
    }

    [Fact]
    public async Task GenerateAsync_WhenRangeExactly366Days_SucceedsAndDelegatesToRepository()
    {
        var from = new DateOnly(2024, 1, 1); // Leap year 2024 has 366 days
        var to = new DateOnly(2024, 12, 31);
        var dto = new GenerateShiftDaysDto(from, to);

        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftGenerationResult(366, 0, 0, 0, 0));

        var service = new ShiftGenerationService(repository.Object);
        var res = await service.GenerateAsync("E", dto, "USER");

        Assert.Equal(366, res.Created);
        repository.Verify(r => r.ExecuteAsync("E", from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(), "USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_WhenRangeExceeds366Days_ThrowsArgumentException()
    {
        var from = new DateOnly(2024, 1, 1);
        var to = from.AddDays(367);
        var dto = new GenerateShiftDaysDto(from, to);

        var repository = new Mock<IShiftGenerationRepository>();
        var service = new ShiftGenerationService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync("E", dto, "USER"));
        Assert.Contains("Generation needs an ordered date range of at most 366 days", ex.Message);
    }

    [Fact]
    public void Plan_WhenShiftDefinitionMissingFromDictionary_ThrowsInvalidOperationException()
    {
        var assignment = Assignment("MISSING_SHIFT");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), new[] { assignment }, Definitions));
        Assert.Contains("Selected shift is missing or inactive", ex.Message);
    }

    [Fact]
    public void Plan_WhenShiftDefinitionIsInactive_ThrowsInvalidOperationException()
    {
        var defsWithInactive = new Dictionary<string, ShiftSchedule>(Definitions)
        {
            ["INACTIVE"] = new()
            {
                ShiftCode = "INACTIVE",
                IsActive = false,
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(17),
                WorkingDaysJson = "[1,2,3,4,5]"
            }
        };

        var assignment = Assignment("INACTIVE");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), new[] { assignment }, defsWithInactive));
        Assert.Contains("Selected shift is missing or inactive", ex.Message);
    }

    [Fact]
    public void Plan_WhenMultipleBaseShiftsWorkOnSameDay_ThrowsInvalidOperationException()
    {
        // Both A and B work on Monday (day 1 of week)
        var assignA = Assignment("A", "BASE");
        var assignB = Assignment("B", "BASE");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), new[] { assignA, assignB }, Definitions));
        Assert.Contains("Conflicting base shifts for this employee/date", ex.Message);
    }

    [Fact]
    public void Plan_WhenMultipleBaseShiftsHaveNonOverlappingWorkingDays_SelectsAppropriateWorkingShift()
    {
        var customDefs = new Dictionary<string, ShiftSchedule>
        {
            ["MON_WED"] = new() { ShiftCode = "MON_WED", IsActive = true, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), WorkingDaysJson = "[1,2,3]" },
            ["THU_SAT"] = new() { ShiftCode = "THU_SAT", IsActive = true, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), WorkingDaysJson = "[4,5,6]" }
        };

        var assign1 = new EmployeeShiftAssignment { EmployeeCode = "E", ShiftCode = "MON_WED", AssignmentKind = "BASE", EffectiveFrom = Start.ToDateTime(TimeOnly.MinValue), EffectiveTo = Start.AddDays(6).ToDateTime(TimeOnly.MinValue), IsActive = true };
        var assign2 = new EmployeeShiftAssignment { EmployeeCode = "E", ShiftCode = "THU_SAT", AssignmentKind = "BASE", EffectiveFrom = Start.ToDateTime(TimeOnly.MinValue), EffectiveTo = Start.AddDays(6).ToDateTime(TimeOnly.MinValue), IsActive = true };

        // Monday (Start) -> MON_WED
        var (monResult, monWrites) = ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), new[] { assign1, assign2 }, customDefs);
        Assert.Equal("MON_WED", Assert.Single(monWrites).ShiftCode);
        Assert.False(monWrites[0].IsRestDay);

        // Thursday (Start + 3) -> THU_SAT
        var (thuResult, thuWrites) = ShiftGenerationService.Plan("E", Start.AddDays(3), Start.AddDays(3), Array.Empty<EmployeeShiftDay>(), new[] { assign1, assign2 }, customDefs);
        Assert.Equal("THU_SAT", Assert.Single(thuWrites).ShiftCode);
        Assert.False(thuWrites[0].IsRestDay);

        // Sunday (Start + 6) -> REST_DAY
        var (sunResult, sunWrites) = ShiftGenerationService.Plan("E", Start.AddDays(6), Start.AddDays(6), Array.Empty<EmployeeShiftDay>(), new[] { assign1, assign2 }, customDefs);
        Assert.Null(Assert.Single(sunWrites).ShiftCode);
        Assert.True(sunWrites[0].IsRestDay);
    }

    [Fact]
    public void Plan_WhenTemporaryAssignmentOverridesBase_EvenWhenTemporaryIsRestDay()
    {
        // Start is Monday. Shift A works Monday.
        // But Temporary Shift T only works Tuesday..Friday [2,3,4,5].
        // Because Temporary has higher priority, Monday becomes a REST_DAY for Temporary!
        var customDefs = new Dictionary<string, ShiftSchedule>(Definitions)
        {
            ["T"] = new() { ShiftCode = "T", IsActive = true, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(18), WorkingDaysJson = "[2,3,4,5]" }
        };

        var baseAssign = Assignment("A", "BASE");
        var tempAssign = Assignment("T", "TEMPORARY");

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, Array.Empty<EmployeeShiftDay>(), new[] { baseAssign, tempAssign }, customDefs);

        var write = Assert.Single(writes);
        Assert.True(write.IsRestDay);
        Assert.Null(write.ShiftCode);
    }

    [Fact]
    public void Plan_WhenExistingGeneratedDayHasDifferentShiftCode_UpdatesIt()
    {
        var existing = new EmployeeShiftDay
        {
            Id = 44,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = "OLD_SHIFT",
            IsRestDay = false,
            IsActive = true,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { existing }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        var write = Assert.Single(writes);
        Assert.Equal(44, write.Id);
        Assert.Equal("A", write.ShiftCode);
        Assert.False(write.IsRestDay);
        Assert.True(write.IsActive);
    }

    [Fact]
    public void Plan_WhenExistingGeneratedDayWasRest_AndPlanBecomesWorking_UpdatesToWorkingDay()
    {
        var existingRest = new EmployeeShiftDay
        {
            Id = 55,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = null,
            IsRestDay = true,
            IsActive = true,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { existingRest }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        var write = Assert.Single(writes);
        Assert.Equal(55, write.Id);
        Assert.Equal("A", write.ShiftCode);
        Assert.False(write.IsRestDay);
    }

    [Fact]
    public void Plan_WhenExistingGeneratedDayWasWorking_AndPlanBecomesRest_UpdatesToRestDay()
    {
        var sunday = Start.AddDays(6);
        var existingWorkingOnSunday = new EmployeeShiftDay
        {
            Id = 66,
            EmployeeCode = "E",
            WorkDate = sunday.ToDateTime(TimeOnly.MinValue),
            ShiftCode = "A",
            IsRestDay = false,
            IsActive = true,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", sunday, sunday, new[] { existingWorkingOnSunday }, new[] { Assignment("A") }, Definitions);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        var write = Assert.Single(writes);
        Assert.Equal(66, write.Id);
        Assert.Null(write.ShiftCode);
        Assert.True(write.IsRestDay);
    }

    [Fact]
    public void Plan_WhenExistingInactiveGeneratedDayInUnscheduledPeriod_LeavesItInactiveWithoutWrites()
    {
        var existingInactive = new EmployeeShiftDay
        {
            Id = 77,
            EmployeeCode = "E",
            WorkDate = Start.ToDateTime(TimeOnly.MinValue),
            ShiftCode = "A",
            IsRestDay = false,
            IsActive = false,
            Source = "GENERATED"
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { existingInactive }, Array.Empty<EmployeeShiftAssignment>(), Definitions);

        Assert.Equal(1, result.Unscheduled);
        Assert.Equal(0, result.Updated);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenAllExistingDaysUnscheduled_DeactivatesAllActiveGeneratedDays()
    {
        var existingList = Enumerable.Range(0, 5).Select(i => new EmployeeShiftDay
        {
            Id = i + 1,
            EmployeeCode = "E",
            WorkDate = Start.AddDays(i).ToDateTime(TimeOnly.MinValue),
            ShiftCode = "A",
            IsRestDay = false,
            IsActive = true,
            Source = "GENERATED"
        }).ToList();

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start.AddDays(4), existingList, Array.Empty<EmployeeShiftAssignment>(), Definitions);

        Assert.Equal(5, result.Unscheduled);
        Assert.Equal(5, result.Updated);
        Assert.Equal(0, result.Created);
        Assert.Equal(5, writes.Count);
        Assert.All(writes, w => Assert.False(w.IsActive));
    }

    [Fact]
    public void Plan_OpenEndedAssignment_GeneratesDaysWithNoEffectiveToEnd()
    {
        var openEnded = new EmployeeShiftAssignment
        {
            EmployeeCode = "E",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = Start.ToDateTime(TimeOnly.MinValue),
            EffectiveTo = null, // open-ended
            IsActive = true
        };

        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start.AddDays(13), Array.Empty<EmployeeShiftDay>(), new[] { openEnded }, Definitions);

        Assert.Equal(14, result.Created);
        Assert.Equal(14, writes.Count);
    }

    [Fact]
    public void Plan_MixedManualAndGeneratedDays_OnlyModifiesGeneratedDays()
    {
        var day1Manual = new EmployeeShiftDay { Id = 1, EmployeeCode = "E", WorkDate = Start.ToDateTime(TimeOnly.MinValue), ShiftCode = "MANUAL_SHIFT", IsRestDay = false, IsActive = true, Source = "MANUAL" };
        var day2ManualRest = new EmployeeShiftDay { Id = 2, EmployeeCode = "E", WorkDate = Start.AddDays(1).ToDateTime(TimeOnly.MinValue), ShiftCode = null, IsRestDay = true, IsActive = true, Source = "MANUAL" };
        var day3Generated = new EmployeeShiftDay { Id = 3, EmployeeCode = "E", WorkDate = Start.AddDays(2).ToDateTime(TimeOnly.MinValue), ShiftCode = "OLD", IsRestDay = false, IsActive = true, Source = "GENERATED" };

        var existing = new[] { day1Manual, day2ManualRest, day3Generated };
        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start.AddDays(3), existing, new[] { Assignment("A") }, Definitions);

        Assert.Equal(2, result.PreservedManual);
        Assert.Equal(1, result.Updated); // day 3
        Assert.Equal(1, result.Created); // day 4
        Assert.Equal(2, writes.Count);

        var updatedDay3 = writes.First(w => w.Id == 3);
        Assert.Equal("A", updatedDay3.ShiftCode);

        var createdDay4 = writes.First(w => w.Id == 0);
        Assert.Equal(Start.AddDays(3).ToDateTime(TimeOnly.MinValue), createdDay4.WorkDate);
    }

    [Fact]
    public async Task GenerateAsync_SingleDay_SucceedsAndCallsRepository()
    {
        var dto = new GenerateShiftDaysDto(Start, Start);
        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", Start.ToDateTime(TimeOnly.MinValue), Start.ToDateTime(TimeOnly.MinValue),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftGenerationResult(1, 0, 0, 0, 0));

        var service = new ShiftGenerationService(repository.Object);
        var res = await service.GenerateAsync("E", dto, "ADMIN");

        Assert.Equal(1, res.Created);
        repository.Verify(r => r.ExecuteAsync("E", Start.ToDateTime(TimeOnly.MinValue), Start.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(), "ADMIN", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_LeapYearFebruary29_ProcessesBoundariesCorrectly()
    {
        var feb28 = new DateOnly(2024, 2, 28);
        var mar1 = new DateOnly(2024, 3, 1); // 3 days: Feb 28, Feb 29, Mar 1
        var dto = new GenerateShiftDaysDto(feb28, mar1);

        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", feb28.ToDateTime(TimeOnly.MinValue), mar1.ToDateTime(TimeOnly.MinValue),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "LEAP_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftGenerationResult(3, 0, 0, 0, 0));

        var service = new ShiftGenerationService(repository.Object);
        var res = await service.GenerateAsync("E", dto, "LEAP_USER");

        Assert.Equal(3, res.Created);
    }

    [Fact]
    public async Task GenerateAsync_PropagatesExactCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var dto = new GenerateShiftDaysDto(Start, Start);
        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", Start.ToDateTime(TimeOnly.MinValue), Start.ToDateTime(TimeOnly.MinValue),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "CT_USER", ct))
            .ReturnsAsync(new ShiftGenerationResult(1, 0, 0, 0, 0));

        var service = new ShiftGenerationService(repository.Object);
        var res = await service.GenerateAsync("E", dto, "CT_USER", ct);

        Assert.Equal(1, res.Created);
        repository.Verify(r => r.ExecuteAsync("E", Start.ToDateTime(TimeOnly.MinValue), Start.ToDateTime(TimeOnly.MinValue),
            It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
            "CT_USER", ct), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var dto = new GenerateShiftDaysDto(Start, Start);
        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", Start.ToDateTime(TimeOnly.MinValue), Start.ToDateTime(TimeOnly.MinValue),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "CANCEL_USER", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftGenerationService(repository.Object);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync("E", dto, "CANCEL_USER", cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_Exactly366DaysDifference_ThrowsArgumentException()
    {
        // DayNumber difference == 366 (367 calendar days)
        var from = new DateOnly(2026, 1, 1);
        var to = from.AddDays(366);
        var dto = new GenerateShiftDaysDto(from, to);
        var repository = new Mock<IShiftGenerationRepository>(MockBehavior.Strict);

        var service = new ShiftGenerationService(repository.Object);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync("E", dto, "USER"));

        Assert.Contains("at most 366 days", ex.Message);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateAsync_Exactly365DaysDifference_Succeeds()
    {
        // DayNumber difference == 365 (366 calendar days)
        var from = new DateOnly(2026, 1, 1);
        var to = from.AddDays(365);
        var dto = new GenerateShiftDaysDto(from, to);
        var repository = new Mock<IShiftGenerationRepository>();
        repository.Setup(r => r.ExecuteAsync("E", from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue),
                It.IsAny<Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (ShiftGenerationResult, IReadOnlyList<EmployeeShiftDay>)>>(),
                "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftGenerationResult(366, 0, 0, 0, 0));

        var service = new ShiftGenerationService(repository.Object);
        var res = await service.GenerateAsync("E", dto, "USER");

        Assert.Equal(366, res.Created);
    }

    [Fact]
    public void Plan_WhenUnscheduledWithExistingInactiveGeneratedDay_LeavesDayUntouched()
    {
        var inactiveDay = Day(0, "GENERATED", active: false);
        var (result, writes) = ShiftGenerationService.Plan("E", Start, Start, new[] { inactiveDay },
            Array.Empty<EmployeeShiftAssignment>(), Definitions);

        Assert.Equal(1, result.Unscheduled);
        Assert.Equal(0, result.Updated);
        Assert.Empty(writes);
    }

    [Fact]
    public void Plan_WhenDateRangeSpansYearEnd_GeneratesCorrectDates()
    {
        var dec30 = new DateOnly(2026, 12, 30);
        var jan02 = new DateOnly(2027, 1, 2);
        var assignment = new EmployeeShiftAssignment
        {
            EmployeeCode = "E",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = dec30.ToDateTime(TimeOnly.MinValue),
            EffectiveTo = jan02.ToDateTime(TimeOnly.MinValue),
            IsActive = true
        };

        var (result, writes) = ShiftGenerationService.Plan("E", dec30, jan02, Array.Empty<EmployeeShiftDay>(),
            new[] { assignment }, Definitions);

        Assert.Equal(4, result.Created);
        Assert.Equal(4, writes.Count);
        Assert.Contains(writes, w => w.WorkDate == dec30.ToDateTime(TimeOnly.MinValue));
        Assert.Contains(writes, w => w.WorkDate == new DateTime(2026, 12, 31));
        Assert.Contains(writes, w => w.WorkDate == new DateTime(2027, 1, 1));
        Assert.Contains(writes, w => w.WorkDate == new DateTime(2027, 1, 2));
    }
}
