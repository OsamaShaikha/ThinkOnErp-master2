using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShiftDefinitionTests
{
    private static WriteShiftDefinitionDto Input() => new("شفت", "Shift", TimeSpan.FromHours(22), TimeSpan.FromHours(6), 45, new[] { 4, 0, 2 }, true);

    [Fact]
    public async Task LegacyInvalidDefinitionCanBeInspectedWithoutInventingAValidPattern()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.GetAsync("BAD", It.IsAny<CancellationToken>())).ReturnsAsync(new ShiftSchedule { ShiftCode = "BAD", WorkingDaysJson = "invalid", StartTime = TimeSpan.Zero, EndTime = TimeSpan.Zero });
        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<string>());
        var result = await new ShiftDefinitionService(repository.Object).GetAsync("BAD");
        Assert.Equal("invalid", result.WorkingDaysJson);
        Assert.Empty(result.WorkingDays);
        Assert.Equal(2, result.ValidationErrors.Count);
        Assert.Equal(64, result.Version.Length);
    }

    [Fact]
    public async Task CreateKeepsCompanyHoursAndCanonicalizesWeekdays()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        ShiftSchedule? captured = null;
        repository.Setup(r => r.CreateAsync(It.IsAny<ShiftSchedule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShiftSchedule s, CancellationToken ct) => { captured = s; return s; });
        var result = await new ShiftDefinitionService(repository.Object).CreateAsync(new(" NIGHT ", Input()), "HR");
        Assert.Equal("NIGHT", result.ShiftCode);
        Assert.Equal(TimeSpan.FromHours(22), result.StartTime);
        Assert.Equal(45, result.BreakMinutes);
        Assert.Equal(new[] { 0, 2, 4 }, result.WorkingDays);
        Assert.Equal("HR", captured!.CreationUser);
        Assert.Equal(64, result.Version.Length);
    }

    [Theory]
    [InlineData("missing-start")]
    [InlineData("missing-active")]
    [InlineData("missing-break")]
    [InlineData("equal-hours")]
    [InlineData("long-clock")]
    [InlineData("negative-break")]
    [InlineData("full-break")]
    [InlineData("duplicate-days")]
    [InlineData("invalid-day")]
    [InlineData("missing-days")]
    public async Task InvalidDefinitionsNeverReachTheRepository(string invalid)
    {
        var input = invalid switch
        {
            "missing-start" => Input() with { StartTime = null }, "missing-active" => Input() with { IsActive = null },
            "missing-break" => Input() with { BreakMinutes = null }, "equal-hours" => Input() with { EndTime = TimeSpan.FromHours(22) },
            "long-clock" => Input() with { StartTime = TimeSpan.FromHours(24) }, "negative-break" => Input() with { BreakMinutes = -1 },
            "full-break" => Input() with { BreakMinutes = 480 }, "duplicate-days" => Input() with { WorkingDays = new[] { 1, 1 } },
            "invalid-day" => Input() with { WorkingDays = new[] { 7 } }, _ => Input() with { WorkingDays = null }
        };
        var repository = new Mock<IShiftDefinitionRepository>();
        var exception = await Record.ExceptionAsync(() => new ShiftDefinitionService(repository.Object).CreateAsync(new("X", input), "HR"));
        Assert.True(exception is ArgumentException or InvalidOperationException);
        repository.Verify(r => r.CreateAsync(It.IsAny<ShiftSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("x/y")]
    [InlineData("x\\y")]
    [InlineData("x\ny")]
    public async Task UnusableRouteCodesAreRejected(string code)
        => await Assert.ThrowsAsync<ArgumentException>(() => new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>()).CreateAsync(new(code, Input()), "HR"));

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/\"ABC\"")]
    public async Task UpdateRequiresASpecificVersion(string version)
        => await Assert.ThrowsAsync<ArgumentException>(() => new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>()).UpdateAsync("X", Input(), version, "HR"));

    [Fact]
    public void ReferencedCalculationIgnoresNamesAndWeekdayOrderButIncludesActiveState()
    {
        var old = new ShiftSchedule { ShiftCode = "X", StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), BreakMinutes = 45, WorkingDaysJson = "[4,0,2]" };
        var proposed = new ShiftSchedule { ShiftCode = "X", NameLocal = "renamed", StartTime = old.StartTime, EndTime = old.EndTime, BreakMinutes = old.BreakMinutes, WorkingDaysJson = "[0,2,4]" };
        Assert.True(ShiftDefinitionRules.SameCalculation(old, proposed));
        proposed.IsActive = false;
        Assert.False(ShiftDefinitionRules.SameCalculation(old, proposed));
        proposed.IsActive = true; proposed.BreakMinutes = 30;
        Assert.False(ShiftDefinitionRules.SameCalculation(old, proposed));
    }

    [Theory]
    [InlineData("not a json")]
    [InlineData("{ \"invalid\": true }")]
    public void WorkingDays_WhenJsonInvalid_ThrowsInvalidOperationException(string json)
    {
        var shift = new ShiftSchedule { WorkingDaysJson = json };
        Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.WorkingDays(shift));
    }

    [Theory]
    [InlineData("[-1, 2, 3]")]
    [InlineData("[0, 1, 7]")]
    public void WorkingDays_WhenDayOutOfRange_ThrowsInvalidOperationException(string json)
    {
        var shift = new ShiftSchedule { WorkingDaysJson = json };
        Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.WorkingDays(shift));
    }

    [Fact]
    public void WorkingDays_WhenDuplicateDays_ThrowsInvalidOperationException()
    {
        var shift = new ShiftSchedule { WorkingDaysJson = "[1, 2, 2, 3]" };
        Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.WorkingDays(shift));
    }

    [Fact]
    public void WorkingDays_ValidArray_ReturnsParsedDays()
    {
        var shift = new ShiftSchedule { WorkingDaysJson = "[0, 1, 2, 3, 4]" };
        var days = ShiftDefinitionRules.WorkingDays(shift);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, days);
    }

    [Fact]
    public void GrossMinutes_StandardDayShift_CalculatesCorrectDuration()
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            BreakMinutes = 60
        };

        // 9 hours = 540 minutes
        Assert.Equal(540m, ShiftDefinitionRules.GrossMinutes(shift));
    }

    [Fact]
    public void GrossMinutes_OvernightShift_CrossesMidnightCorrectly()
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(22, 0, 0),
            EndTime = new TimeSpan(6, 0, 0),
            BreakMinutes = 45
        };

        // 22:00 to 06:00 = 8 hours = 480 minutes
        Assert.Equal(480m, ShiftDefinitionRules.GrossMinutes(shift));
    }

    [Fact]
    public void GrossMinutes_WhenStartTimeEqualsEndTime_ThrowsInvalidOperationException()
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(8, 0, 0),
            BreakMinutes = 0
        };

        Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.GrossMinutes(shift));
    }

    [Fact]
    public void GrossMinutes_WhenBreakExceedsOrEqualsDuration_ThrowsInvalidOperationException()
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(12, 0, 0), // 4 hours = 240 minutes
            BreakMinutes = 240 // Break equals total duration
        };

        Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.GrossMinutes(shift));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListAsync_WhenPageOrSizeInvalid_ThrowsArgumentException(int page, int size)
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(true, null, page, size));
    }

    [Fact]
    public async Task ListAsync_WhenSearchTooLong_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var service = new ShiftDefinitionService(repository.Object);
        var search = new string('A', 201);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(true, search, 1, 10));
    }

    [Fact]
    public async Task ListAsync_HappyPath_ReturnsMappedPageWithReferencedStatus()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var shift = new ShiftSchedule
        {
            ShiftCode = "DAY_SHIFT",
            NameLocal = "شفت صباحي",
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            BreakMinutes = 30,
            WorkingDaysJson = "[0,1,2,3,4]",
            IsActive = true
        };

        repository.Setup(r => r.ListAsync(true, "DAY", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ShiftSchedule> { shift }, 1));

        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "DAY_SHIFT" });

        var service = new ShiftDefinitionService(repository.Object);
        var page = await service.ListAsync(true, "DAY", 1, 10);

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("DAY_SHIFT", item.ShiftCode);
        Assert.True(item.IsInUse);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task GetAsync_WhenCodeContainsPathSeparators_ThrowsArgumentException(string code)
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(code));
    }

    [Fact]
    public async Task GetAsync_WhenShiftNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.GetAsync("MISSING", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShiftSchedule?)null);

        var service = new ShiftDefinitionService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync("MISSING"));
        Assert.Contains("Shift not found", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchInvalid_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync("SHIFT1", "INVALID_VERSION"));
        repository.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_DelegatesToRepository()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var service = new ShiftDefinitionService(repository.Object);
        var validVersion = new string('F', 64);

        repository.Setup(r => r.DeleteAsync("SHIFT1", validVersion, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.DeleteAsync("SHIFT1", validVersion);
        repository.Verify(r => r.DeleteAsync("SHIFT1", validVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!, "USER"));
    }

    [Fact]
    public async Task CreateAsync_WhenDefinitionInDtoNull_ThrowsArgumentNullException()
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(new("SHIFT", null!), "USER"));
    }

    [Theory]
    [InlineData(null, "Shift En")]
    [InlineData("", "Shift En")]
    [InlineData("   ", "Shift En")]
    [InlineData("شفت", null)]
    [InlineData("شفت", "")]
    [InlineData("شفت", "   ")]
    public async Task CreateAsync_WhenNameLocalOrEnNullOrWhitespace_ThrowsArgumentException(string? nameLocal, string? nameEn)
    {
        var dto = new CreateShiftDefinitionDto("SHIFT1", Input() with { NameLocal = nameLocal!, NameEn = nameEn! });
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, "USER"));
        Assert.Contains("Both shift names are required", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenNameLocalOrEnExceeds200Chars_ThrowsArgumentException()
    {
        var longName = new string('A', 201);
        var dtoLocal = new CreateShiftDefinitionDto("SHIFT1", Input() with { NameLocal = longName });
        var dtoEn = new CreateShiftDefinitionDto("SHIFT1", Input() with { NameEn = longName });
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dtoLocal, "USER"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dtoEn, "USER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task CreateAsync_WhenShiftCodeNullEmptyOrDot_ThrowsArgumentException(string? code)
    {
        var dto = new CreateShiftDefinitionDto(code!, Input());
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, "USER"));
        Assert.Contains("Shift code is required", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenShiftCodeExceeds50Chars_ThrowsArgumentException()
    {
        var longCode = new string('X', 51);
        var dto = new CreateShiftDefinitionDto(longCode, Input());
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, "USER"));
        Assert.Contains("up to 50 characters", ex.Message);
    }

    [Theory]
    [InlineData("SHIFT\t1")]
    [InlineData("SHIFT\n1")]
    [InlineData("SHIFT\r1")]
    [InlineData("SHIFT\01")]
    public async Task CreateAsync_WhenShiftCodeContainsControlCharacters_ThrowsArgumentException(string code)
    {
        var dto = new CreateShiftDefinitionDto(code, Input());
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, "USER"));
        Assert.Contains("without path separators or control characters", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateAsync("SHIFT1", null!, new string('A', 64), "USER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 63 chars
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 65 chars
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAG")] // 'G' is non-hex
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA?")]
    public async Task UpdateAsync_WhenIfMatchInvalid_ThrowsArgumentException(string? ifMatch)
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("SHIFT1", Input(), ifMatch!, "USER"));
        Assert.Contains("If-Match must contain one shift version from GET", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenIfMatchEnclosedInQuotes_TrimsAndSucceeds()
    {
        var repository = new Mock<IShiftDefinitionRepository>();
        var hexVersion = new string('A', 64);
        var quotedVersion = "\"" + hexVersion + "\"";

        var updatedShift = new ShiftSchedule
        {
            ShiftCode = "SHIFT1",
            NameLocal = "شفت محدث",
            NameEn = "Updated Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            BreakMinutes = 30,
            WorkingDaysJson = "[0,1,2,3,4]",
            IsActive = true,
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.UpdateAsync("SHIFT1", It.IsAny<ShiftSchedule>(), hexVersion, "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedShift);
        repository.Setup(r => r.GetAsync("SHIFT1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedShift);
        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var service = new ShiftDefinitionService(repository.Object);
        var result = await service.UpdateAsync("SHIFT1", Input() with { NameLocal = "شفت محدث", NameEn = "Updated Shift" }, quotedVersion, "USER");

        Assert.Equal("SHIFT1", result.ShiftCode);
        Assert.Equal("شفت محدث", result.NameLocal);
        repository.Verify(r => r.UpdateAsync("SHIFT1", It.IsAny<ShiftSchedule>(), hexVersion, "USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("..")]
    public async Task DeleteAsync_WhenCodeInvalid_ThrowsArgumentException(string code)
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(code, new string('A', 64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NOT_HEX")]
    public async Task DeleteAsync_WhenIfMatchInvalid_ThrowsArgumentException2(string? ifMatch)
    {
        var service = new ShiftDefinitionService(Mock.Of<IShiftDefinitionRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync("SHIFT1", ifMatch!));
    }

    [Fact]
    public void SameCalculation_WhenStartTimeDiffers_ReturnsFalse()
    {
        var a = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "[0,1]", IsActive = true };
        var b = new ShiftSchedule { StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "[0,1]", IsActive = true };
        Assert.False(ShiftDefinitionRules.SameCalculation(a, b));
    }

    [Fact]
    public void SameCalculation_WhenEndTimeDiffers_ReturnsFalse()
    {
        var a = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "[0,1]", IsActive = true };
        var b = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), BreakMinutes = 30, WorkingDaysJson = "[0,1]", IsActive = true };
        Assert.False(ShiftDefinitionRules.SameCalculation(a, b));
    }

    [Fact]
    public void SameCalculation_WhenWorkingDaysDiffer_ReturnsFalse()
    {
        var a = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "[0,1]", IsActive = true };
        var b = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "[0,2]", IsActive = true };
        Assert.False(ShiftDefinitionRules.SameCalculation(a, b));
    }

    [Fact]
    public void SameCalculation_WhenJsonCorrupted_ComparesRawJsonString()
    {
        var a = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "bad_json_1", IsActive = true };
        var b = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "bad_json_1", IsActive = true };
        var c = new ShiftSchedule { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), BreakMinutes = 30, WorkingDaysJson = "bad_json_2", IsActive = true };

        Assert.True(ShiftDefinitionRules.SameCalculation(a, b));
        Assert.False(ShiftDefinitionRules.SameCalculation(a, c));
    }

    [Theory]
    [InlineData(8, 0, 16, 30, 30, 510)]   // 08:00 to 16:30 = 8.5h = 510m
    [InlineData(16, 0, 0, 0, 60, 480)]    // 16:00 to 00:00 (cross midnight) = 8h = 480m
    [InlineData(23, 0, 7, 0, 45, 480)]    // 23:00 to 07:00 = 8h = 480m
    [InlineData(9, 0, 13, 0, 15, 240)]    // 09:00 to 13:00 = 4h = 240m
    [InlineData(0, 0, 23, 59, 120, 1439)] // 00:00 to 23:59 = 1439m
    public void GrossMinutes_Theory_ComputesExactElapsedMinutes(int startH, int startM, int endH, int endM, int breakMin, decimal expectedGross)
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(startH, startM, 0),
            EndTime = new TimeSpan(endH, endM, 0),
            BreakMinutes = breakMin
        };

        Assert.Equal(expectedGross, ShiftDefinitionRules.GrossMinutes(shift));
    }

    [Fact]
    public void GrossMinutes_WhenBreakMinutesNegative_ThrowsInvalidOperationException()
    {
        var shift = new ShiftSchedule
        {
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            BreakMinutes = -1
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.GrossMinutes(shift));
        Assert.Contains("Shift break must be nonnegative and shorter than the shift", ex.Message);
    }

    [Theory]
    [InlineData(-1, 0, 16, 0)]
    [InlineData(25, 0, 16, 0)]
    [InlineData(8, 0, -1, 0)]
    [InlineData(8, 0, 24, 0)]
    public void GrossMinutes_WhenStartOrEndTimeOutOfOneDayBounds_ThrowsInvalidOperationException(int sH, int sM, int eH, int eM)
    {
        var shift = new ShiftSchedule
        {
            StartTime = new TimeSpan(sH, sM, 0),
            EndTime = new TimeSpan(eH, eM, 0),
            BreakMinutes = 30
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ShiftDefinitionRules.GrossMinutes(shift));
        Assert.Contains("must be distinct clock times within one day", ex.Message);
    }

    [Fact]
    public void WorkingDays_WhenArrayContainsAllSevenDays_ReturnsDistinctDays0Through6()
    {
        var shift = new ShiftSchedule { WorkingDaysJson = "[0, 1, 2, 3, 4, 5, 6]" };
        var days = ShiftDefinitionRules.WorkingDays(shift);
        Assert.Equal(7, days.Length);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, days);
    }

    [Fact]
    public void Version_IsDeterministicAndChangesWhenAnyFieldChanges()
    {
        var baseDate = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var s1 = new ShiftSchedule
        {
            ShiftCode = "S1",
            NameLocal = "شفت",
            NameEn = "Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            BreakMinutes = 30,
            WorkingDaysJson = "[0,1,2,3,4]",
            IsActive = true,
            CreationDate = baseDate,
            UpdateDate = null
        };

        var s2 = new ShiftSchedule
        {
            ShiftCode = "S1",
            NameLocal = "شفت",
            NameEn = "Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            BreakMinutes = 30,
            WorkingDaysJson = "[0,1,2,3,4]",
            IsActive = true,
            CreationDate = baseDate,
            UpdateDate = null
        };

        var v1 = ShiftDefinitionRules.Version(s1);
        var v2 = ShiftDefinitionRules.Version(s2);
        Assert.Equal(v1, v2);

        // Change each property one by one
        s2.ShiftCode = "S2";
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.ShiftCode = s1.ShiftCode;

        s2.NameLocal = "شفت معدل";
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.NameLocal = s1.NameLocal;

        s2.NameEn = "Modified Shift";
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.NameEn = s1.NameEn;

        s2.StartTime = TimeSpan.FromHours(9);
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.StartTime = s1.StartTime;

        s2.EndTime = TimeSpan.FromHours(17);
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.EndTime = s1.EndTime;

        s2.BreakMinutes = 45;
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.BreakMinutes = s1.BreakMinutes;

        s2.WorkingDaysJson = "[1,2,3,4,5]";
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.WorkingDaysJson = s1.WorkingDaysJson;

        s2.IsActive = false;
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.IsActive = s1.IsActive;

        s2.CreationDate = baseDate.AddDays(1);
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
        s2.CreationDate = s1.CreationDate;

        s2.UpdateDate = baseDate.AddHours(2);
        Assert.NotEqual(v1, ShiftDefinitionRules.Version(s2));
    }

    [Fact]
    public async Task ShiftDefinitionService_ListAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.ListAsync(true, "SHIFT", 1, 10, cts.Token))
            .ReturnsAsync((new List<ShiftSchedule>(), 0));

        var service = new ShiftDefinitionService(repository.Object);
        var result = await service.ListAsync(true, "SHIFT", 1, 10, cts.Token);

        Assert.NotNull(result);
        repository.Verify(r => r.ListAsync(true, "SHIFT", 1, 10, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ShiftDefinitionService_ListAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.ListAsync(true, "SHIFT", 1, 10, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ListAsync(true, "SHIFT", 1, 10, cts.Token));
    }

    [Fact]
    public async Task ShiftDefinitionService_GetAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IShiftDefinitionRepository>();
        var shift = new ShiftSchedule { ShiftCode = "S1", WorkingDaysJson = "[0,1,2,3,4]", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) };

        repository.Setup(r => r.GetAsync("S1", cts.Token))
            .ReturnsAsync(shift);
        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token))
            .ReturnsAsync(new HashSet<string>());

        var service = new ShiftDefinitionService(repository.Object);
        var result = await service.GetAsync("S1", cts.Token);

        Assert.Equal("S1", result.ShiftCode);
        repository.Verify(r => r.GetAsync("S1", cts.Token), Times.Once);
        repository.Verify(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task ShiftDefinitionService_GetAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.GetAsync("S1", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetAsync("S1", cts.Token));
    }

    [Fact]
    public async Task ShiftDefinitionService_CreateAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.CreateAsync(It.IsAny<ShiftSchedule>(), cts.Token))
            .ReturnsAsync((ShiftSchedule s, CancellationToken _) => s);
        repository.Setup(r => r.GetAsync("SHIFT1", cts.Token))
            .ReturnsAsync(new ShiftSchedule { ShiftCode = "SHIFT1", WorkingDaysJson = "[0,1,2,3,4]", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) });
        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token))
            .ReturnsAsync(new HashSet<string>());

        var service = new ShiftDefinitionService(repository.Object);
        var dto = new CreateShiftDefinitionDto("SHIFT1", Input());
        var result = await service.CreateAsync(dto, "HR_USER", cts.Token);

        Assert.Equal("SHIFT1", result.ShiftCode);
        repository.Verify(r => r.CreateAsync(It.IsAny<ShiftSchedule>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task ShiftDefinitionService_CreateAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IShiftDefinitionRepository>();
        repository.Setup(r => r.CreateAsync(It.IsAny<ShiftSchedule>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftDefinitionService(repository.Object);
        var dto = new CreateShiftDefinitionDto("SHIFT1", Input());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateAsync(dto, "HR_USER", cts.Token));
    }

    [Fact]
    public async Task ShiftDefinitionService_UpdateAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IShiftDefinitionRepository>();
        var hexVersion = new string('A', 64);
        var shift = new ShiftSchedule { ShiftCode = "SHIFT1", WorkingDaysJson = "[0,1,2,3,4]", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) };

        repository.Setup(r => r.UpdateAsync("SHIFT1", It.IsAny<ShiftSchedule>(), hexVersion, "HR_USER", cts.Token))
            .ReturnsAsync(shift);
        repository.Setup(r => r.GetAsync("SHIFT1", cts.Token))
            .ReturnsAsync(shift);
        repository.Setup(r => r.ReferencedCodesAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token))
            .ReturnsAsync(new HashSet<string>());

        var service = new ShiftDefinitionService(repository.Object);
        var result = await service.UpdateAsync("SHIFT1", Input(), hexVersion, "HR_USER", cts.Token);

        Assert.Equal("SHIFT1", result.ShiftCode);
        repository.Verify(r => r.UpdateAsync("SHIFT1", It.IsAny<ShiftSchedule>(), hexVersion, "HR_USER", cts.Token), Times.Once);
    }

    [Fact]
    public async Task ShiftDefinitionService_UpdateAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IShiftDefinitionRepository>();
        var hexVersion = new string('A', 64);

        repository.Setup(r => r.UpdateAsync("SHIFT1", It.IsAny<ShiftSchedule>(), hexVersion, "HR_USER", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UpdateAsync("SHIFT1", Input(), hexVersion, "HR_USER", cts.Token));
    }

    [Fact]
    public async Task ShiftDefinitionService_DeleteAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IShiftDefinitionRepository>();
        var hexVersion = new string('B', 64);

        repository.Setup(r => r.DeleteAsync("SHIFT1", hexVersion, cts.Token))
            .Returns(Task.CompletedTask);

        var service = new ShiftDefinitionService(repository.Object);
        await service.DeleteAsync("SHIFT1", hexVersion, cts.Token);

        repository.Verify(r => r.DeleteAsync("SHIFT1", hexVersion, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ShiftDefinitionService_DeleteAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = new Mock<IShiftDefinitionRepository>();
        var hexVersion = new string('B', 64);

        repository.Setup(r => r.DeleteAsync("SHIFT1", hexVersion, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new ShiftDefinitionService(repository.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DeleteAsync("SHIFT1", hexVersion, cts.Token));
    }
}
