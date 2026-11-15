using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeaveBalanceMigrationTests
{
    [Fact]
    public async Task DraftRequiresExplicitAllocationsAndPreservesEveryBalanceComponent()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("cutover-2026", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetAllLeaveBalancesForMigrationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new LeaveBalance { Id = 42, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
                CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
                AccruedDays = 16m, UsedDays = 4m, CarriedForwardDays = 3m } });
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        LeaveBalanceMigration? saved = null;
        repository.Setup(r => r.SaveLeaveBalanceMigrationDraftAsync(It.IsAny<LeaveBalanceMigration>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalanceMigration, CancellationToken>((m, _) => { m.Id = 12; saved = m; }).ReturnsAsync((LeaveBalanceMigration m, CancellationToken _) => m);
        var service = new LeaveService(repository.Object, employee.Object);
        var dto = new CreateLeaveBalanceMigrationDto("cutover-2026", new DateTime(2026, 4, 1), 4, 1,
        [
            new(42, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 8m, 2m, 1m),
            new(42, new DateTime(2026, 4, 1), new DateTime(2027, 3, 31), 8m, 2m, 2m)
        ]);

        var result = await service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN");

        Assert.Equal(12, result.Id);
        Assert.Equal("DRAFT", result.Status);
        Assert.Equal(2, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Equal(42, line.SourceBalanceId));
        Assert.Equal(16m, result.Lines.Sum(l => l.AllocatedAccruedDays));
        Assert.Equal(4m, result.Lines.Sum(l => l.AllocatedUsedDays));
        Assert.Equal(3m, result.Lines.Sum(l => l.AllocatedCarriedDays));
        Assert.NotNull(saved);
        Assert.All(saved!.Lines, line => Assert.Equal(2025, line.SourceCycleStartDate.Year));
    }

    [Fact]
    public async Task DraftRejectsAnyUnallocatedBalanceAndAnyChangedComponentTotal()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetAllLeaveBalancesForMigrationAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new LeaveBalance { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "A", YearNo = 2025, AccruedDays = 10m },
            new LeaveBalance { Id = 2, EmployeeCode = "E2", LeaveTypeCode = "A", YearNo = 2025, AccruedDays = 5m }
        });
        var service = new LeaveService(repository.Object, employee.Object);
        var oneAllocation = new CreateLeaveBalanceMigrationDto("m1", new DateTime(2026, 1, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);
        var wrongTotals = oneAllocation with
        {
            MigrationKey = "m2",
            Allocations = [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 9m, 0m, 0m),
                new(2, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 5m, 0m, 0m)]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveBalanceMigrationDraftAsync(oneAllocation, "HR_ADMIN"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveBalanceMigrationDraftAsync(wrongTotals, "HR_ADMIN"));
        repository.Verify(r => r.SaveLeaveBalanceMigrationDraftAsync(It.IsAny<LeaveBalanceMigration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraft_IdempotentRetryReturnsExistingMigrationWhenPayloadMatches()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var dto = new CreateLeaveBalanceMigrationDto("key-idem", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            MigrationKey = "key-idem",
            EffectiveDate = dto.EffectiveDate.Date,
            dto.TargetYearStartMonth,
            dto.TargetYearStartDay,
            Allocations = dto.Allocations.Select(a => new
            {
                a.SourceBalanceId,
                Start = a.TargetCycleStartDate.Date,
                End = a.TargetCycleEndDate.Date,
                Accrued = a.AccruedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                Used = a.UsedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                Carried = a.CarriedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
            })
        });
        var expectedHash = LeaveMovementKey.Hash(payload);

        var existing = new LeaveBalanceMigration
        {
            Id = 55,
            MigrationKey = "key-idem",
            PayloadHash = expectedHash,
            Status = "DRAFT",
            EffectiveDate = new DateTime(2026, 4, 1),
            TargetYearStartMonth = 4,
            TargetYearStartDay = 1,
            Lines = [new LeaveBalanceMigrationLine
            {
                SourceBalanceId = 1,
                EmployeeCode = "E1",
                LeaveTypeCode = "ANNUAL",
                SourceCycleStartDate = new DateTime(2025, 1, 1),
                SourceCycleEndDate = new DateTime(2025, 12, 31),
                TargetCycleStartDate = new DateTime(2025, 4, 1),
                TargetCycleEndDate = new DateTime(2026, 3, 31),
                AllocatedAccruedDays = 10m
            }]
        };
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-idem", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = new LeaveService(repository.Object, employee.Object);
        var result = await service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN");

        Assert.Equal(55, result.Id);
        Assert.Equal("DRAFT", result.Status);
        repository.Verify(r => r.SaveLeaveBalanceMigrationDraftAsync(It.IsAny<LeaveBalanceMigration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraft_ConflictingPayloadForSameKeyThrows()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var dto = new CreateLeaveBalanceMigrationDto("key-conflict", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);

        var existing = new LeaveBalanceMigration
        {
            Id = 56,
            MigrationKey = "key-conflict",
            PayloadHash = "different-hash",
            Status = "DRAFT"
        };
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-conflict", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = new LeaveService(repository.Object, employee.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("MigrationKey was already used for a different reviewed allocation plan", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_RejectsWhenTargetSettingMatchesCurrent()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-match", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 4, LeaveYearStartDay = 1 });

        var service = new LeaveService(repository.Object, employee.Object);
        var dto = new CreateLeaveBalanceMigrationDto("key-match", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("target leave-year start matches the current setting", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_RejectsWhenNoBalancesToMigrate()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-empty", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetAllLeaveBalancesForMigrationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LeaveBalance>());

        var service = new LeaveService(repository.Object, employee.Object);
        var dto = new CreateLeaveBalanceMigrationDto("key-empty", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("no existing leave balances to migrate", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_RejectsUnknownSourceBalance()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-unknown", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetAllLeaveBalancesForMigrationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LeaveBalance { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025 }]);

        var service = new LeaveService(repository.Object, employee.Object);
        var dto = new CreateLeaveBalanceMigrationDto("key-unknown", new DateTime(2026, 4, 1), 4, 1,
            [
                new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 0m, 0m, 0m),
                new(999, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)
            ]);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("outside the current tenant", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_RejectsInvalidTargetCycleBoundaries()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetLeaveBalanceMigrationByKeyAsync("key-bad-bound", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMigration?)null);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetAllLeaveBalancesForMigrationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LeaveBalance { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025, AccruedDays = 10m }]);

        var service = new LeaveService(repository.Object, employee.Object);
        // Target start month is May (5) instead of configured April (4)
        var dto = new CreateLeaveBalanceMigrationDto("key-bad-bound", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 5, 1), new DateTime(2026, 4, 30), 10m, 0m, 0m)]);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("configured target leave-year boundary", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_RejectsMoreThanFourDecimalPlaces()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var service = new LeaveService(repository.Object, employee.Object);

        var dto = new CreateLeaveBalanceMigrationDto("key-precision", new DateTime(2026, 4, 1), 4, 1,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10.12345m, 0m, 0m)]);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
        Assert.Contains("at most four decimal places", ex.Message);
    }

    [Theory]
    [InlineData("", 4, 1)]
    [InlineData("   ", 4, 1)]
    [InlineData("key", 0, 1)]
    [InlineData("key", 13, 1)]
    [InlineData("key", 4, 0)]
    [InlineData("key", 4, 32)]
    public async Task CreateDraft_RejectsInvalidInputArguments(string migrationKey, int targetMonth, int targetDay)
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var service = new LeaveService(repository.Object, employee.Object);

        var dto = new CreateLeaveBalanceMigrationDto(migrationKey, new DateTime(2026, 4, 1), targetMonth, targetDay,
            [new(1, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31), 10m, 0m, 0m)]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));
    }

    [Fact]
    public async Task CreateDraft_RejectsNullAllocationEntryBeforeReadingOrSavingMigration()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var service = new LeaveService(repository.Object, employee.Object);
        var dto = new CreateLeaveBalanceMigrationDto("key-null-allocation", new DateTime(2026, 4, 1), 4, 1,
            new LeaveBalanceMigrationAllocationDto[] { null! });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLeaveBalanceMigrationDraftAsync(dto, "HR_ADMIN"));

        Assert.Equal("Allocations", exception.ParamName);
        repository.Verify(r => r.GetLeaveBalanceMigrationByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveLeaveBalanceMigrationDraftAsync(It.IsAny<LeaveBalanceMigration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_CallsRepositoryApplyAndReturnsMappedDto()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var migration = new LeaveBalanceMigration
        {
            Id = 12, MigrationKey = "key-post", Status = "POSTED", EffectiveDate = new DateTime(2026, 4, 1),
            TargetYearStartMonth = 4, TargetYearStartDay = 1, PostedBy = "APPROVER", PostedDate = DateTime.UtcNow
        };
        repository.Setup(r => r.ApplyLeaveBalanceMigrationAsync(12, "APPROVER", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(migration);

        var service = new LeaveService(repository.Object, employee.Object);
        var result = await service.PostLeaveBalanceMigrationAsync(12, "APPROVER");

        Assert.Equal(12, result.Id);
        Assert.Equal("POSTED", result.Status);
        Assert.Equal("APPROVER", result.PostedBy);
        repository.Verify(r => r.ApplyLeaveBalanceMigrationAsync(12, "APPROVER", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_ThrowsKeyNotFoundWhenMigrationDoesNotExist()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.ApplyLeaveBalanceMigrationAsync(999, "APPROVER", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalanceMigration?)null);

        var service = new LeaveService(repository.Object, employee.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.PostLeaveBalanceMigrationAsync(999, "APPROVER"));
    }

    [Fact]
    public async Task Reverse_CallsRepositoryApplyWithReverseTrueAndReturnsMappedDto()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        var migration = new LeaveBalanceMigration
        {
            Id = 12, MigrationKey = "key-rev", Status = "REVERSED", EffectiveDate = new DateTime(2026, 4, 1),
            TargetYearStartMonth = 4, TargetYearStartDay = 1, ReversedBy = "REVERSER", ReversedDate = DateTime.UtcNow
        };
        repository.Setup(r => r.ApplyLeaveBalanceMigrationAsync(12, "REVERSER", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(migration);

        var service = new LeaveService(repository.Object, employee.Object);
        var result = await service.ReverseLeaveBalanceMigrationAsync(12, "REVERSER");

        Assert.Equal(12, result.Id);
        Assert.Equal("REVERSED", result.Status);
        Assert.Equal("REVERSER", result.ReversedBy);
        repository.Verify(r => r.ApplyLeaveBalanceMigrationAsync(12, "REVERSER", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverse_ThrowsKeyNotFoundWhenMigrationDoesNotExist()
    {
        var repository = new Mock<ILeaveRepository>();
        var employee = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.ApplyLeaveBalanceMigrationAsync(999, "REVERSER", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalanceMigration?)null);

        var service = new LeaveService(repository.Object, employee.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReverseLeaveBalanceMigrationAsync(999, "REVERSER"));
    }
}
