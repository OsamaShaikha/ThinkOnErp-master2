using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShiftAssignmentTests
{
    private static EmployeeShiftAssignment Row(int from, int to) => new() { EmployeeCode = "E1", ShiftCode = "A", AssignmentKind = "BASE",
        EffectiveFrom = new DateTime(2026, 1, from), EffectiveTo = new DateTime(2026, 1, to) };

    [Theory]
    [InlineData(1, 10, 1, 10)]
    [InlineData(1, 10, 1, 20)]
    [InlineData(1, 20, 1, 10)]
    [InlineData(1, 20, 5, 15)]
    [InlineData(5, 15, 1, 20)]
    [InlineData(1, 5, 20, 31)]
    [InlineData(20, 31, 1, 5)]
    [InlineData(1, 20, 10, 31)]
    public void DateOnlyChangesProtectExactlyTheSymmetricDifference(int oldFrom, int oldTo, int newFrom, int newTo)
    {
        var affected = ShiftAssignmentRules.Affected(Row(oldFrom, oldTo), Row(newFrom, newTo));
        for (var day = 1; day <= 31; day++)
        {
            var date = new DateTime(2026, 1, day);
            var expected = (day >= oldFrom && day <= oldTo) != (day >= newFrom && day <= newTo);
            Assert.Equal(expected, affected.Any(r => r.Start <= date && r.End >= date));
        }
    }

    [Fact]
    public void EndingAnOpenLegacyBaseOnlyAffectsItsFutureTail()
    {
        var old = Row(1, 31); old.EffectiveTo = null; old.AssignmentKind = null;
        var proposed = Row(1, 31);
        var changed = Assert.Single(ShiftAssignmentRules.Affected(old, proposed));
        Assert.Equal(new DateTime(2026, 2, 1), changed.Start);
        Assert.Equal(DateTime.MaxValue.Date, changed.End);
    }

    [Theory]
    [InlineData(false, "BASE")]
    [InlineData(true, "TEMPORARY")]
    public void EquivalentLegacyClassificationDoesNotChangeTheSchedule(bool bounded, string kind)
    {
        var old = Row(1, 31); old.AssignmentKind = null; if (!bounded) old.EffectiveTo = null;
        var proposed = Row(1, 31); proposed.AssignmentKind = kind; proposed.EffectiveTo = old.EffectiveTo;
        Assert.Empty(ShiftAssignmentRules.Affected(old, proposed));
    }

    [Fact]
    public void ReinterpretingABoundedLegacyRowAsBaseProtectsTheWholePeriod()
    {
        var old = Row(1, 31); old.AssignmentKind = null;
        var affected = ShiftAssignmentRules.Affected(old, Row(1, 31));
        Assert.Contains(affected, r => r.Start == old.EffectiveFrom && r.End == old.EffectiveTo);
    }

    [Theory]
    [InlineData(1, 5, 5, 10, 1, true)] // Jan 5 2026 = Monday.
    [InlineData(1, 4, 5, 10, 1, false)]
    [InlineData(1, 1, 1, 1, 1, false)] // No Monday in a one-day Thursday overlap.
    [InlineData(1, 31, 1, 31, 1, true)]
    public void BaseCollisionRequiresAnActualCommonWorkingDate(int a, int b, int c, int d, int weekday, bool expected)
        => Assert.Equal(expected, ShiftAssignmentRules.WorkingOverlap(ShiftAssignmentRules.Range(Row(a, b)), new[] { weekday }, ShiftAssignmentRules.Range(Row(c, d)), new[] { weekday }));

    [Fact]
    public void ComplementaryWeekdaysCanShareABasePeriod()
        => Assert.False(ShiftAssignmentRules.WorkingOverlap(ShiftAssignmentRules.Range(Row(1, 31)), new[] { 0, 1, 2, 3, 4 }, ShiftAssignmentRules.Range(Row(1, 31)), new[] { 5, 6 }));

    private static WriteShiftAssignmentDto Input() => new("A", "BASE", new DateOnly(2026, 1, 1), null, true, new string('A', 64));

    [Theory]
    [InlineData("kind")]
    [InlineData("temporary-end")]
    [InlineData("order")]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("code")]
    [InlineData("version")]
    public async Task InvalidWritesAreRejectedBeforeTheRepository(string problem)
    {
        var dto = problem switch
        {
            "kind" => Input() with { AssignmentKind = "UNKNOWN" }, "temporary-end" => Input() with { AssignmentKind = "TEMPORARY" },
            "order" => Input() with { EffectiveTo = new DateOnly(2025, 12, 31) }, "start" => Input() with { EffectiveFrom = null },
            "active" => Input() with { IsActive = null }, "code" => Input() with { ShiftCode = "" }, _ => Input() with { ShiftVersion = "" }
        };
        var repository = new Mock<IShiftAssignmentRepository>();
        await Assert.ThrowsAsync<ArgumentException>(() => new ShiftAssignmentService(repository.Object).CreateAsync("E1", dto, "HR"));
        repository.Verify(r => r.CreateAsync(It.IsAny<EmployeeShiftAssignment>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadRejectsAnEmployeeOutsideTheScopeBeforeReadingAssignments()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("E1", It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ShiftAssignmentService(repository.Object).GetAsync("E1", 1));
        repository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseVersion_WhenNullOrWhitespace_ThrowsArgumentException(string? value)
    {
        var ex = Assert.Throws<ArgumentException>(() => ShiftAssignmentRules.ParseVersion(value));
        Assert.Contains("A specific version from GET is required", ex.Message);
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")] // 63 chars
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")] // 65 chars
    public void ParseVersion_WhenLengthNot64_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => ShiftAssignmentRules.ParseVersion(value));
    }

    [Fact]
    public void ParseVersion_WhenNonHexCharacter_ThrowsArgumentException()
    {
        var invalid = new string('A', 63) + "Z"; // 'Z' is not hex
        Assert.Throws<ArgumentException>(() => ShiftAssignmentRules.ParseVersion(invalid));
    }

    [Fact]
    public void ParseVersion_WhenValidHex64_ReturnsUppercase()
    {
        var valid = new string('a', 64);
        var parsed = ShiftAssignmentRules.ParseVersion(valid);
        Assert.Equal(new string('A', 64), parsed);
    }

    [Fact]
    public void ParseVersion_WhenEnclosedInQuotes_TrimsQuotesAndValidates()
    {
        var valid = $"\"{new string('f', 64)}\"";
        var parsed = ShiftAssignmentRules.ParseVersion(valid);
        Assert.Equal(new string('F', 64), parsed);
    }

    [Fact]
    public void Kind_WhenNullAndEffectiveToHasValue_ReturnsTemporary()
    {
        var row = Row(1, 10);
        row.AssignmentKind = null;
        row.EffectiveTo = new DateTime(2026, 1, 10);
        Assert.Equal("TEMPORARY", ShiftAssignmentRules.Kind(row));
    }

    [Fact]
    public void Kind_WhenNullAndEffectiveToNull_ReturnsBase()
    {
        var row = Row(1, 10);
        row.AssignmentKind = null;
        row.EffectiveTo = null;
        Assert.Equal("BASE", ShiftAssignmentRules.Kind(row));
    }

    [Fact]
    public void Kind_WhenTemporaryWithoutEffectiveTo_ThrowsInvalidOperationException()
    {
        var row = Row(1, 10);
        row.AssignmentKind = "TEMPORARY";
        row.EffectiveTo = null;
        Assert.Throws<InvalidOperationException>(() => ShiftAssignmentRules.Kind(row));
    }

    [Fact]
    public void Kind_WhenInvalidString_ThrowsInvalidOperationException()
    {
        var row = Row(1, 10);
        row.AssignmentKind = "UNKNOWN_KIND";
        Assert.Throws<InvalidOperationException>(() => ShiftAssignmentRules.Kind(row));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListAsync_WhenPageOrSizeInvalid_ThrowsArgumentException(int page, int size)
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("E1", null, null, false, page, size));
    }

    [Fact]
    public async Task ListAsync_WhenFromDateAfterToDate_ThrowsArgumentException()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);
        var from = new DateOnly(2026, 2, 1);
        var to = new DateOnly(2026, 1, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("E1", from, to, false, 1, 10));
    }

    [Fact]
    public async Task ListAsync_HappyPath_ReturnsMappedPageWithDefinitions()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var assignment = new EmployeeShiftAssignment
        {
            Id = 10,
            EmployeeCode = "E1",
            ShiftCode = "SHIFT_MORNING",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        repository.Setup(r => r.ListAsync("E1", null, null, true, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<EmployeeShiftAssignment> { assignment }, 1));

        var shiftDef = new ShiftSchedule
        {
            ShiftCode = "SHIFT_MORNING",
            NameLocal = "شفت صباحي",
            IsActive = true
        };

        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule> { { "SHIFT_MORNING", shiftDef } });

        var service = new ShiftAssignmentService(repository.Object);
        var page = await service.ListAsync("E1", null, null, true, 1, 20);

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("SHIFT_MORNING", item.ShiftCode);
        Assert.Equal("BASE", item.AssignmentKind);
        Assert.True(item.ShiftIsActive);
    }

    [Fact]
    public async Task GetAsync_WhenAssignmentNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        repository.Setup(r => r.EnsureEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetAsync("E1", 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeShiftAssignment?)null);

        var service = new ShiftAssignmentService(repository.Object);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync("E1", 999));
        Assert.Contains("Shift assignment not found", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchInvalid_ThrowsArgumentExceptionBeforeRepository()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync("E1", 1, "INVALID_VERSION"));
        repository.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenIfMatchValid_DelegatesToRepository()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);
        var validVersion = new string('A', 64);

        repository.Setup(r => r.DeleteAsync("E1", 1, validVersion, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.DeleteAsync("E1", 1, validVersion);
        repository.Verify(r => r.DeleteAsync("E1", 1, validVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenValidInput_CallsRepositoryCreateAndReturnsMapped()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var dto = new WriteShiftAssignmentDto(
            "SHIFT_A",
            "BASE",
            new DateOnly(2026, 1, 1),
            null,
            true,
            new string('A', 64)
        );

        var createdEntity = new EmployeeShiftAssignment
        {
            Id = 42,
            EmployeeCode = "E1",
            ShiftCode = "SHIFT_A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = DateTime.UtcNow
        };

        repository.Setup(r => r.CreateAsync(It.IsAny<EmployeeShiftAssignment>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        repository.Setup(r => r.EnsureEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repository.Setup(r => r.GetAsync("E1", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>
            {
                ["SHIFT_A"] = new() { ShiftCode = "SHIFT_A", NameLocal = "شفت أ", IsActive = true }
            });

        var service = new ShiftAssignmentService(repository.Object);
        var result = await service.CreateAsync("E1", dto, "HR_ADMIN");

        Assert.Equal(42, result.Id);
        Assert.Equal("SHIFT_A", result.ShiftCode);
        Assert.Equal("BASE", result.AssignmentKind);
        Assert.True(result.IsActive);
        repository.Verify(r => r.CreateAsync(It.Is<EmployeeShiftAssignment>(a => a.EmployeeCode == "E1" && a.ShiftCode == "SHIFT_A"), new string('A', 64), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenValidInput_CallsRepositoryUpdateAndReturnsMapped()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var dto = new WriteShiftAssignmentDto(
            "SHIFT_B",
            "TEMPORARY",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            true,
            new string('B', 64)
        );

        var ifMatch = new string('C', 64);

        var updatedEntity = new EmployeeShiftAssignment
        {
            Id = 55,
            EmployeeCode = "E1",
            ShiftCode = "SHIFT_B",
            AssignmentKind = "TEMPORARY",
            EffectiveFrom = new DateTime(2026, 2, 1),
            EffectiveTo = new DateTime(2026, 2, 28),
            IsActive = true,
            CreationDate = DateTime.UtcNow,
            UpdateDate = DateTime.UtcNow
        };

        repository.Setup(r => r.UpdateAsync("E1", 55, It.IsAny<EmployeeShiftAssignment>(), new string('C', 64), new string('B', 64), "HR_ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEntity);

        repository.Setup(r => r.EnsureEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repository.Setup(r => r.GetAsync("E1", 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEntity);

        repository.Setup(r => r.DefinitionsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ShiftSchedule>
            {
                ["SHIFT_B"] = new() { ShiftCode = "SHIFT_B", NameLocal = "شفت ب", IsActive = true }
            });

        var service = new ShiftAssignmentService(repository.Object);
        var result = await service.UpdateAsync("E1", 55, dto, ifMatch, "HR_ADMIN");

        Assert.Equal(55, result.Id);
        Assert.Equal("SHIFT_B", result.ShiftCode);
        Assert.Equal("TEMPORARY", result.AssignmentKind);
        repository.Verify(r => r.UpdateAsync("E1", 55, It.IsAny<EmployeeShiftAssignment>(), new string('C', 64), new string('B', 64), "HR_ADMIN", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ShiftAssignmentRules_Range_WhenEffectiveToNull_ReturnsDateTimeMaxValueDate()
    {
        var assignment = new EmployeeShiftAssignment
        {
            EffectiveFrom = new DateTime(2026, 3, 1),
            EffectiveTo = null
        };

        var range = ShiftAssignmentRules.Range(assignment);

        Assert.Equal(new DateTime(2026, 3, 1), range.Start);
        Assert.Equal(DateTime.MaxValue.Date, range.End);
    }

    [Fact]
    public void ShiftAssignmentRules_Range_WhenEffectiveToSet_ReturnsExactEndDate()
    {
        var assignment = new EmployeeShiftAssignment
        {
            EffectiveFrom = new DateTime(2026, 3, 1),
            EffectiveTo = new DateTime(2026, 3, 31)
        };

        var range = ShiftAssignmentRules.Range(assignment);

        Assert.Equal(new DateTime(2026, 3, 1), range.Start);
        Assert.Equal(new DateTime(2026, 3, 31), range.End);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-10", "2026-01-15", "2026-01-20", false)]
    [InlineData("2026-01-01", "2026-01-10", "2026-01-10", "2026-01-15", true)] // Touches on 10th
    [InlineData("2026-01-05", "2026-01-15", "2026-01-01", "2026-01-20", true)] // Fully contained
    public void ShiftAssignmentRules_Overlap_EvaluatesDateBoundariesCorrectly(string s1, string e1, string s2, string e2, bool expected)
    {
        var r1 = new HrDateRange(DateTime.Parse(s1), DateTime.Parse(e1));
        var r2 = new HrDateRange(DateTime.Parse(s2), DateTime.Parse(e2));

        Assert.Equal(expected, ShiftAssignmentRules.Overlap(r1, r2));
        Assert.Equal(expected, ShiftAssignmentRules.Overlap(r2, r1));
    }

    [Fact]
    public void ShiftAssignmentRules_Affected_WhenShiftCodeDiffers_ReturnsBothFullRanges()
    {
        var old = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = true };
        var proposed = new EmployeeShiftAssignment { ShiftCode = "B", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = true };

        var affected = ShiftAssignmentRules.Affected(old, proposed);

        Assert.Equal(2, affected.Count);
        Assert.Contains(affected, r => r.Start == old.EffectiveFrom && r.End == old.EffectiveTo);
        Assert.Contains(affected, r => r.Start == proposed.EffectiveFrom && r.End == proposed.EffectiveTo);
    }

    [Fact]
    public void ShiftAssignmentRules_Affected_WhenIsActiveDiffers_ReturnsBothFullRanges()
    {
        var old = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = true };
        var proposed = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = false };

        var affected = ShiftAssignmentRules.Affected(old, proposed);

        Assert.Equal(2, affected.Count);
    }

    [Fact]
    public void ShiftAssignmentRules_Affected_WhenKindDiffers_ReturnsBothFullRanges()
    {
        var old = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = true };
        var proposed = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "TEMPORARY", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 31), IsActive = true };

        var affected = ShiftAssignmentRules.Affected(old, proposed);

        Assert.Equal(2, affected.Count);
    }

    [Fact]
    public void ShiftAssignmentRules_Affected_WhenCompletelyDisjoint_ReturnsBothRanges()
    {
        var old = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 1, 5), IsActive = true };
        var proposed = new EmployeeShiftAssignment { ShiftCode = "A", AssignmentKind = "BASE", EffectiveFrom = new DateTime(2026, 1, 20), EffectiveTo = new DateTime(2026, 1, 25), IsActive = true };

        var affected = ShiftAssignmentRules.Affected(old, proposed);

        Assert.Equal(2, affected.Count);
    }

    [Fact]
    public void ShiftAssignmentRules_Version_IsDeterministicForSameValues()
    {
        var a1 = new EmployeeShiftAssignment
        {
            Id = 1,
            EmployeeCode = "EMP01",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var a2 = new EmployeeShiftAssignment
        {
            Id = 1,
            EmployeeCode = "EMP01",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal(ShiftAssignmentRules.Version(a1), ShiftAssignmentRules.Version(a2));
        Assert.Equal(64, ShiftAssignmentRules.Version(a1).Length);
    }

    [Theory]
    [InlineData("ShiftCode")]
    [InlineData("EffectiveFrom")]
    [InlineData("IsActive")]
    [InlineData("AssignmentKind")]
    public void ShiftAssignmentRules_Version_DiffersWhenAnyFieldChanges(string fieldToChange)
    {
        var baseAssign = new EmployeeShiftAssignment
        {
            Id = 1,
            EmployeeCode = "EMP01",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var modified = new EmployeeShiftAssignment
        {
            Id = 1,
            EmployeeCode = "EMP01",
            ShiftCode = fieldToChange == "ShiftCode" ? "B" : "A",
            AssignmentKind = fieldToChange == "AssignmentKind" ? "TEMPORARY" : "BASE",
            EffectiveFrom = fieldToChange == "EffectiveFrom" ? new DateTime(2026, 1, 2) : new DateTime(2026, 1, 1),
            EffectiveTo = fieldToChange == "AssignmentKind" ? new DateTime(2026, 1, 10) : null,
            IsActive = fieldToChange != "IsActive",
            CreationDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        Assert.NotEqual(ShiftAssignmentRules.Version(baseAssign), ShiftAssignmentRules.Version(modified));
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("EmployeeCode")]
    [InlineData("CreationDate")]
    [InlineData("UpdateDate")]
    public void ShiftAssignmentRules_Version_AuditFieldsAffectHash(string fieldToChange)
    {
        var baseAssign = new EmployeeShiftAssignment
        {
            Id = 1,
            EmployeeCode = "EMP01",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var modified = new EmployeeShiftAssignment
        {
            Id = fieldToChange == "Id" ? 2 : 1,
            EmployeeCode = fieldToChange == "EmployeeCode" ? "EMP02" : "EMP01",
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreationDate = fieldToChange == "CreationDate" ? new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc) : new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            UpdateDate = fieldToChange == "UpdateDate" ? new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc) : null
        };

        Assert.NotEqual(ShiftAssignmentRules.Version(baseAssign), ShiftAssignmentRules.Version(modified));
    }

    [Fact]
    public void ShiftAssignmentRules_WorkingOverlap_WhenRangesOverlap_ButNoCommonWorkingDays_ReturnsFalse()
    {
        var r1 = new HrDateRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 7));
        var r2 = new HrDateRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 7));

        // Monday (1) and Tuesday (2) vs Wednesday (3) and Thursday (4)
        var overlap = ShiftAssignmentRules.WorkingOverlap(r1, new[] { 1, 2 }, r2, new[] { 3, 4 });
        Assert.False(overlap);
    }

    [Fact]
    public void ShiftAssignmentRules_WorkingOverlap_WhenRangesDoNotOverlap_ReturnsFalse()
    {
        var r1 = new HrDateRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 5));
        var r2 = new HrDateRange(new DateTime(2026, 1, 10), new DateTime(2026, 1, 15));

        var overlap = ShiftAssignmentRules.WorkingOverlap(r1, new[] { 1, 2, 3 }, r2, new[] { 1, 2, 3 });
        Assert.False(overlap);
    }

    [Fact]
    public void ShiftAssignmentRules_Affected_WhenBothOpenEnded_AndEffectiveFromDiffers_ReturnsDifferenceRange()
    {
        var old = new EmployeeShiftAssignment
        {
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = null,
            IsActive = true
        };
        var proposed = new EmployeeShiftAssignment
        {
            ShiftCode = "A",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 1, 15),
            EffectiveTo = null,
            IsActive = true
        };

        var affected = ShiftAssignmentRules.Affected(old, proposed);

        var diff = Assert.Single(affected);
        Assert.Equal(new DateTime(2026, 1, 1), diff.Start);
        Assert.Equal(new DateTime(2026, 1, 14), diff.End);
    }

    [Fact]
    public async Task ShiftAssignmentService_CreateAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.Setup(r => r.CreateAsync(It.IsAny<EmployeeShiftAssignment>(), It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var dto = new WriteShiftAssignmentDto("SHIFT_A", "BASE", new DateOnly(2026, 1, 1), null, true, new string('A', 64));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CreateAsync("E1", dto, "USER", cts.Token));
    }

    [Fact]
    public async Task ShiftAssignmentService_UpdateAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.Setup(r => r.UpdateAsync("E1", 5, It.IsAny<EmployeeShiftAssignment>(), It.IsAny<string>(), It.IsAny<string>(), "USER", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var dto = new WriteShiftAssignmentDto("SHIFT_A", "BASE", new DateOnly(2026, 1, 1), null, true, new string('A', 64));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.UpdateAsync("E1", 5, dto, new string('A', 64), "USER", cts.Token));
    }

    [Fact]
    public async Task ShiftAssignmentService_DeleteAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.Setup(r => r.DeleteAsync("E1", 5, It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.DeleteAsync("E1", 5, new string('A', 64), cts.Token));
    }

    [Fact]
    public async Task ShiftAssignmentService_GetAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.Setup(r => r.EnsureEmployeeAsync("E1", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetAsync("E1", 5, cts.Token));
    }

    [Fact]
    public async Task ShiftAssignmentService_ListAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IShiftAssignmentRepository>();
        var service = new ShiftAssignmentService(repository.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        repository.Setup(r => r.EnsureEmployeeAsync("E1", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ListAsync("E1", null, null, false, 1, 10, cts.Token));
    }
}
