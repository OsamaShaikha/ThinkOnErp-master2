using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShiftDayTests
{
    private static WriteShiftDayDto Valid => new(new DateOnly(2026, 12, 1), "A", false, true, new string('A', 64));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public async Task InvalidInputNeverReachesPersistence(int scenario)
    {
        var dto = scenario switch
        {
            0 => Valid with { WorkDate = null }, 1 => Valid with { IsActive = null }, 2 => Valid with { IsRestDay = null },
            3 => Valid with { ShiftCode = null }, 4 => Valid with { ShiftCode = " " },
            5 => Valid with { IsRestDay = true }, 6 => Valid with { ShiftVersion = null },
            7 => Valid with { ShiftCode = new string('S', 101) },
            _ => Valid with { IsRestDay = true, ShiftCode = null } // version cannot accompany rest
        };
        var repository = new Mock<IShiftDayRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new ShiftDayService(repository.Object).CreateAsync("E", dto, "U"));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OutOfScopeReadsStopBeforeReadingRows(bool list)
    {
        var repository = new Mock<IShiftDayRepository>(MockBehavior.Strict);
        repository.Setup(r => r.EnsureEmployeeAsync("E", default)).ThrowsAsync(new KeyNotFoundException());
        var service = new ShiftDayService(repository.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            if (list) await service.ListAsync("E", null, null, false, 1, 50);
            else await service.GetAsync("E", 1);
        });
        repository.Verify(r => r.EnsureEmployeeAsync("E", default), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EditingGeneratedRestExplicitlyBecomesManualAndKeepsRouteEmployee()
    {
        var repository = new Mock<IShiftDayRepository>();
        var row = new EmployeeShiftDay { Id = 5, EmployeeCode = "E", WorkDate = new DateTime(2026, 12, 1), IsRestDay = true, Source = "GENERATED" };
        repository.Setup(r => r.GetAsync("E", 5, default)).ReturnsAsync(row);
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<IReadOnlyList<string>>(), default)).ReturnsAsync(new Dictionary<string, ShiftSchedule>());
        repository.Setup(r => r.UpdateAsync("E", 5, It.IsAny<EmployeeShiftDay>(), new string('A', 64), null, "U", default))
            .Callback<string, long, EmployeeShiftDay, string, string?, string, CancellationToken>((employee, id, proposed, version, shift, actor, ct) =>
            {
                Assert.Equal("MANUAL", proposed.Source); Assert.Equal(employee, proposed.EmployeeCode);
                Assert.True(proposed.IsRestDay); Assert.Null(proposed.ShiftCode);
            }).ReturnsAsync(row);
        await new ShiftDayService(repository.Object).UpdateAsync("E", 5, new(new DateOnly(2026, 12, 1), null, true, false, null), new string('A', 64), "U");
        repository.Verify(r => r.UpdateAsync("E", 5, It.IsAny<EmployeeShiftDay>(), It.IsAny<string>(), null, "U", default), Times.Once);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void VersionDetectsOperationalChanges(int change)
    {
        var row = new EmployeeShiftDay { Id = 1, EmployeeCode = "E", ShiftCode = "A", WorkDate = new DateTime(2026, 12, 1) };
        var version = ShiftDayRules.Version(row);
        switch (change)
        {
            case 0: row.WorkDate = row.WorkDate.AddDays(1); break;
            case 1: row.ShiftCode = "B"; break;
            case 2: row.IsRestDay = true; break;
            case 3: row.IsActive = false; break;
            case 4: row.Source = "GENERATED"; break;
        }
        Assert.NotEqual(version, ShiftDayRules.Version(row));
    }

    [Fact]
    public void Validate_WhenWorkDateHasTimePart_ThrowsArgumentException()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1, 10, 30, 0),
            Source = "MANUAL",
            IsRestDay = true
        };
        Assert.Throws<ArgumentException>(() => ShiftDayRules.Validate(day));
    }

    [Fact]
    public void Validate_WhenSourceIsNotManual_ThrowsArgumentException()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "GENERATED",
            IsRestDay = true
        };
        Assert.Throws<ArgumentException>(() => ShiftDayRules.Validate(day));
    }

    [Fact]
    public void Validate_WhenRestDayHasShiftCode_ThrowsArgumentException()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "MANUAL",
            IsRestDay = true,
            ShiftCode = "SHIFT_A"
        };
        Assert.Throws<ArgumentException>(() => ShiftDayRules.Validate(day));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenWorkingDayHasNullOrWhitespaceShiftCode_ThrowsArgumentException(string? shiftCode)
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "MANUAL",
            IsRestDay = false,
            ShiftCode = shiftCode
        };
        Assert.Throws<ArgumentException>(() => ShiftDayRules.Validate(day));
    }

    [Fact]
    public void Validate_WhenWorkingDayShiftCodeExceeds100Chars_ThrowsArgumentException()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "MANUAL",
            IsRestDay = false,
            ShiftCode = new string('S', 101)
        };
        Assert.Throws<ArgumentException>(() => ShiftDayRules.Validate(day));
    }

    [Fact]
    public void Validate_HappyPath_WorkingDay_Succeeds()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "MANUAL",
            IsRestDay = false,
            ShiftCode = "SHIFT_MORNING"
        };
        ShiftDayRules.Validate(day); // No exception
    }

    [Fact]
    public void Validate_HappyPath_RestDay_Succeeds()
    {
        var day = new EmployeeShiftDay
        {
            WorkDate = new DateTime(2026, 1, 1),
            Source = "MANUAL",
            IsRestDay = true,
            ShiftCode = null
        };
        ShiftDayRules.Validate(day); // No exception
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListAsync_WhenPageOrSizeInvalid_ThrowsArgumentException(int page, int size)
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("EMP1", null, null, false, page, size));
    }

    [Fact]
    public async Task ListAsync_WhenFromDateAfterToDate_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        var from = new DateOnly(2026, 5, 10);
        var to = new DateOnly(2026, 5, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("EMP1", from, to, false, 1, 10));
    }

    [Fact]
    public async Task ListAsync_HappyPath_ReturnsMappedPageWithDefinitions()
    {
        var repository = new Mock<IShiftDayRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var day = new EmployeeShiftDay
        {
            Id = 1,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 5, 1),
            ShiftCode = "SHIFT_DAY",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        var shiftDef = new ShiftSchedule
        {
            ShiftCode = "SHIFT_DAY",
            NameLocal = "شفت نهاري",
            IsActive = true
        };

        repository.Setup(r => r.ListAsync("EMP1", null, null, true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<EmployeeShiftDay> { day }, 1));

        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule> { { "SHIFT_DAY", shiftDef } });

        var service = new ShiftDayService(repository.Object);
        var page = await service.ListAsync("EMP1", null, null, true, 1, 10);

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("EMP1", item.EmployeeCode);
        Assert.Equal("SHIFT_DAY", item.ShiftCode);
        Assert.False(item.IsRestDay);
        Assert.True(item.ShiftIsActive);
    }

    [Fact]
    public async Task GetAsync_WhenDayNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IShiftDayRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetAsync("EMP1", 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeShiftDay?)null);

        var service = new ShiftDayService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync("EMP1", 999));
        Assert.Contains("Daily roster entry not found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_HappyPath_ReturnsMappedDto()
    {
        var repository = new Mock<IShiftDayRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var day = new EmployeeShiftDay
        {
            Id = 22,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 6, 1),
            ShiftCode = "NIGHT",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        repository.Setup(r => r.GetAsync("EMP1", 22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(day);
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>());

        var service = new ShiftDayService(repository.Object);
        var dto = await service.GetAsync("EMP1", 22);

        Assert.Equal(22, dto.Id);
        Assert.Equal("EMP1", dto.EmployeeCode);
        Assert.Equal(new DateOnly(2026, 6, 1), dto.WorkDate);
        Assert.Equal("NIGHT", dto.ShiftCode);
    }

    [Fact]
    public async Task CreateAsync_WhenWorkingDayWithoutShiftVersion_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        var dto = new WriteShiftDayDto(new DateOnly(2026, 7, 1), "SHIFT_A", false, true, ShiftVersion: null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("EMP1", dto, "USER"));
        Assert.Contains("A specific version from GET is required", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenRestDayWithShiftVersion_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        var dto = new WriteShiftDayDto(new DateOnly(2026, 7, 1), null, true, true, ShiftVersion: new string('A', 64));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("EMP1", dto, "USER"));
        Assert.Contains("Rest entries cannot specify a shift version", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchInvalid_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync("EMP1", 1, "INVALID_VERSION"));
        repository.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchValid_DelegatesToRepository()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        var validVersion = new string('B', 64);

        repository.Setup(r => r.DeleteAsync("EMP1", 1, validVersion, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.DeleteAsync("EMP1", 1, validVersion);
        repository.Verify(r => r.DeleteAsync("EMP1", 1, validVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchEnclosedInQuotes_TrimsAndDelegatesToRepository()
    {
        var repository = new Mock<IShiftDayRepository>();
        var service = new ShiftDayService(repository.Object);
        var hex = new string('C', 64);
        var quoted = "\"" + hex + "\"";

        repository.Setup(r => r.DeleteAsync("EMP1", 5, hex, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.DeleteAsync("EMP1", 5, quoted);
        repository.Verify(r => r.DeleteAsync("EMP1", 5, hex, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new ShiftDayService(Mock.Of<IShiftDayRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync("EMP1", null!, "USER"));
    }

    [Fact]
    public async Task CreateAsync_RestDay_WithNullShiftCode_CallsCreateWithNullDefinitionVersion()
    {
        var repository = new Mock<IShiftDayRepository>();
        var created = new EmployeeShiftDay
        {
            Id = 10,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 12, 1),
            ShiftCode = null,
            IsRestDay = true,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.CreateAsync(It.IsAny<EmployeeShiftDay>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetAsync("EMP1", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>());

        var service = new ShiftDayService(repository.Object);
        var dto = new WriteShiftDayDto(new DateOnly(2026, 12, 1), null, true, true, ShiftVersion: null);

        var result = await service.CreateAsync("EMP1", dto, "ADMIN");

        Assert.Equal(10, result.Id);
        Assert.True(result.IsRestDay);
        Assert.Null(result.ShiftCode);
        repository.Verify(r => r.CreateAsync(It.Is<EmployeeShiftDay>(d => d.IsRestDay && d.ShiftCode == null && d.Source == "MANUAL"), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WorkingDay_CallsCreateWithParsedDefinitionVersion()
    {
        var repository = new Mock<IShiftDayRepository>();
        var defVersion = new string('D', 64);
        var created = new EmployeeShiftDay
        {
            Id = 11,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 12, 1),
            ShiftCode = "SHIFT_MORNING",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.CreateAsync(It.IsAny<EmployeeShiftDay>(), defVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetAsync("EMP1", 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>());

        var service = new ShiftDayService(repository.Object);
        var dto = new WriteShiftDayDto(new DateOnly(2026, 12, 1), "SHIFT_MORNING", false, true, ShiftVersion: defVersion);

        var result = await service.CreateAsync("EMP1", dto, "ADMIN");

        Assert.Equal(11, result.Id);
        Assert.False(result.IsRestDay);
        Assert.Equal("SHIFT_MORNING", result.ShiftCode);
        repository.Verify(r => r.CreateAsync(It.Is<EmployeeShiftDay>(d => !d.IsRestDay && d.ShiftCode == "SHIFT_MORNING" && d.Source == "MANUAL"), defVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new ShiftDayService(Mock.Of<IShiftDayRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateAsync("EMP1", 1, null!, new string('A', 64), "USER"));
    }

    [Fact]
    public async Task UpdateAsync_WhenRestDayWithShiftVersion_ThrowsArgumentException()
    {
        var service = new ShiftDayService(Mock.Of<IShiftDayRepository>());
        var dto = new WriteShiftDayDto(new DateOnly(2026, 12, 1), null, true, true, ShiftVersion: new string('A', 64));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("EMP1", 1, dto, new string('B', 64), "USER"));
        Assert.Contains("Rest entries cannot specify a shift version", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkingDayWithoutShiftVersion_ThrowsArgumentException()
    {
        var service = new ShiftDayService(Mock.Of<IShiftDayRepository>());
        var dto = new WriteShiftDayDto(new DateOnly(2026, 12, 1), "SHIFT_A", false, true, ShiftVersion: null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("EMP1", 1, dto, new string('B', 64), "USER"));
        Assert.Contains("A specific version from GET is required", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_HappyPath_UpdatesAndReturnsMappedDto()
    {
        var repository = new Mock<IShiftDayRepository>();
        var ifMatch = new string('A', 64);
        var defVersion = new string('D', 64);

        var updated = new EmployeeShiftDay
        {
            Id = 15,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 12, 1),
            ShiftCode = "SHIFT_NIGHT",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.UpdateAsync("EMP1", 15, It.IsAny<EmployeeShiftDay>(), ifMatch, defVersion, "OPERATOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetAsync("EMP1", 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>());

        var service = new ShiftDayService(repository.Object);
        var dto = new WriteShiftDayDto(new DateOnly(2026, 12, 1), "SHIFT_NIGHT", false, true, ShiftVersion: defVersion);

        var result = await service.UpdateAsync("EMP1", 15, dto, ifMatch, "OPERATOR");

        Assert.Equal(15, result.Id);
        Assert.Equal("SHIFT_NIGHT", result.ShiftCode);
        repository.Verify(r => r.UpdateAsync("EMP1", 15, It.IsAny<EmployeeShiftDay>(), ifMatch, defVersion, "OPERATOR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_WhenDefinitionsDoNotContainShift_MapsShiftDefinitionVersionAsNull()
    {
        var repository = new Mock<IShiftDayRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("EMP1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var day = new EmployeeShiftDay
        {
            Id = 1,
            EmployeeCode = "EMP1",
            WorkDate = new DateTime(2026, 5, 1),
            ShiftCode = "UNKNOWN_SHIFT",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.ListAsync("EMP1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<EmployeeShiftDay> { day }, 1));
        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>()); // Shift not found

        var service = new ShiftDayService(repository.Object);
        var page = await service.ListAsync("EMP1", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), true, 1, 10);

        var item = Assert.Single(page.Items);
        Assert.Null(item.ShiftVersion);
        Assert.Null(item.ShiftIsActive);
    }

    [Fact]
    public void ShiftDayVersionConflictException_InstantiatesWithDefaultMessage()
    {
        var ex = new ShiftDayVersionConflictException();
        Assert.Contains("Daily roster version changed", ex.Message);
    }

    [Fact]
    public void Version_IsDeterministicAndChangesWhenAnyFieldChanges()
    {
        var baseDate = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var d1 = new EmployeeShiftDay
        {
            Id = 100,
            EmployeeCode = "E1",
            WorkDate = new DateTime(2026, 11, 10),
            ShiftCode = "S1",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = baseDate,
            UpdateDate = null
        };

        var d2 = new EmployeeShiftDay
        {
            Id = 100,
            EmployeeCode = "E1",
            WorkDate = new DateTime(2026, 11, 10),
            ShiftCode = "S1",
            IsRestDay = false,
            IsActive = true,
            Source = "MANUAL",
            CreationDate = baseDate,
            UpdateDate = null
        };

        var v1 = ShiftDayRules.Version(d1);
        var v2 = ShiftDayRules.Version(d2);
        Assert.Equal(v1, v2);

        // Mutate each property
        d2.Id = 101;
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.Id = d1.Id;

        d2.EmployeeCode = "E2";
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.EmployeeCode = d1.EmployeeCode;

        d2.WorkDate = d1.WorkDate.AddDays(1);
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.WorkDate = d1.WorkDate;

        d2.ShiftCode = "S2";
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.ShiftCode = d1.ShiftCode;

        d2.IsRestDay = true;
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.IsRestDay = d1.IsRestDay;

        d2.IsActive = false;
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.IsActive = d1.IsActive;

        d2.Source = "GENERATED";
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.Source = d1.Source;

        d2.CreationDate = baseDate.AddDays(1);
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
        d2.CreationDate = d1.CreationDate;

        d2.UpdateDate = baseDate.AddHours(1);
        Assert.NotEqual(v1, ShiftDayRules.Version(d2));
    }
}
