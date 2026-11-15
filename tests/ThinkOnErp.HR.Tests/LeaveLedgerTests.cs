using Microsoft.EntityFrameworkCore;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LeaveLedgerTests
{
    [Theory]
    [InlineData(10, 2, 3, 0, 2, 0, 11)]
    [InlineData(2, 2, 0, -8, 0, -3, 0)]
    public void MovementExplainsAllThreeBalanceBuckets(decimal accrued, decimal used, decimal carried,
        decimal accruedDelta, decimal usedDelta, decimal carriedDelta, decimal available)
    {
        var balance = new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "A", YearNo = 2026, AccruedDays = accrued, UsedDays = used, CarriedForwardDays = carried };
        var before = new LeaveBalanceState(accrued - accruedDelta, used - usedDelta, carried - carriedDelta);
        var movement = LeaveBalanceMovement.Create(balance, before, "TEST", "key", "source", "reason", "HR", 1);
        Assert.Equal(accruedDelta, movement.AccruedDelta);
        Assert.Equal(usedDelta, movement.UsedDelta);
        Assert.Equal(carriedDelta, movement.CarriedDelta);
        Assert.Equal(available, movement.AccruedAfter + movement.CarriedAfter - movement.UsedAfter);
        Assert.Equal(before.Available + accruedDelta + carriedDelta - usedDelta, available);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SavedMovementsCannotBeChangedOrDeleted(EntityState state)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle("User Id=unused;Password=unused;Data Source=unused").Options);
        db.Entry(new LeaveBalanceMovement { Id = 1, BalanceId = 1 }).State = state;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", error.Message);
    }

    [Fact]
    public async Task ManualRetryReturnsOriginalResultAndRejectsAChangedPayload()
    {
        var repository = new Mock<ILeaveRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var balance = new LeaveBalance { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "A", YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1), AccruedDays = 5, UsedDays = 1 };
        var movements = new Dictionary<string, LeaveBalanceMovement>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string e, Func<Task<LeaveBalanceDto>> action, CancellationToken ct) => action());
        repository.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken ct) => movements.GetValueOrDefault(key));
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1", HireDate = new DateTime(2020, 1, 1) });
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveType { LeaveTypeCode = "A" });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, LeaveBalanceState, string, string, string, string, string, string?, decimal, decimal, long?, CancellationToken>((b, before, kind, key, source, reason, actor, hash, expired, transfer, reverse, ct) =>
            {
                var movement = LeaveBalanceMovement.Create(b, before, kind, key, source, reason, actor, 1);
                movement.PayloadHash = hash;
                movements.Add(key, movement);
            }).Returns(Task.CompletedTask);
        var service = new LeaveService(repository.Object, employees.Object);
        var request = new GrantLeaveBalanceDto("E1", "A", 2026, 2, "MANUAL_GRANT", "correction", "stable-request-id");
        var first = await service.GrantOrAdjustBalanceAsync(request, "HR");
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Closed cycle"));
        var retry = await service.GrantOrAdjustBalanceAsync(request with { Days = 2.0000m }, "HR");
        Assert.Equal(7m, balance.AccruedDays);
        Assert.Equal(first, retry);
        Assert.Single(movements);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantOrAdjustBalanceAsync(request with { Days = 3 }, "HR"));
        Assert.Equal(7m, balance.AccruedDays);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManualCorrectionCanBeReversedOnceWithRequiredReasonAndIdempotentRetry()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var balance = new LeaveBalance { Id = 9, EmployeeCode = "E1", LeaveTypeCode = "A", YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1), CycleEndDate = new DateTime(2026, 12, 31), AccruedDays = 7, UsedDays = 1 };
        var original = new LeaveBalanceMovement
        {
            Id = 22, BalanceId = 9, EmployeeCode = "E1", LeaveTypeCode = "A", CycleStartDate = new DateTime(2026, 1, 1),
            SequenceNo = 3, Kind = "CORRECTION", Source = "MANUAL:adjustment-22",
            AccruedDelta = 2, AccruedAfter = 7, UsedAfter = 1, CarriedAfter = 0
        };
        var movements = new Dictionary<string, LeaveBalanceMovement>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        repository.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => movements.GetValueOrDefault(key));
        repository.Setup(r => r.HasMovementReversalAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.HasActiveMovementAfterAsync(9, 3L, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveType { LeaveTypeCode = "A", NameLocal = "Annual" });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetPendingLeaveDaysAsync("E1", "A", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), null, It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        repository.Setup(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, LeaveBalanceState, string, string, string, string, string, string?, decimal, decimal, long?, CancellationToken>(
                (b, before, kind, key, source, reason, actor, payload, expired, transferred, reversesId, _) =>
                {
                    var reversal = LeaveBalanceMovement.Create(b, before, kind, key, source, reason, actor, 4);
                    reversal.PayloadHash = payload;
                    reversal.ReversesMovementId = reversesId;
                    movements.Add(key, reversal);
                }).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new LeaveService(repository.Object, employeeRepo.Object);
        var request = new ReverseLeaveBalanceMovementDto("  Approved correction was entered in error  ");
        var first = await service.ReverseManualBalanceMovementAsync("E1", 22, request, "HR");
        Assert.Equal(5m, balance.AccruedDays);
        Assert.Equal(4m, first.RemainingDays);
        var reversal = Assert.Single(movements.Values);
        Assert.Equal("REVERSAL", reversal.Kind);
        Assert.Equal(22, reversal.ReversesMovementId);
        Assert.Equal("Approved correction was entered in error", reversal.Reason);

        var retry = await service.ReverseManualBalanceMovementAsync("E1", 22, request, "HR");
        Assert.Equal(first, retry);
        repository.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManualCorrectionCannotBeReversedWhileLaterMovementIsActive()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveBalanceMovement
        {
            Id = 22, BalanceId = 9, EmployeeCode = "E1", LeaveTypeCode = "A", SequenceNo = 3,
            Kind = "CORRECTION", Source = "MANUAL:original", AccruedDelta = 2, AccruedAfter = 7
        });
        repository.Setup(r => r.HasMovementReversalAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.HasActiveMovementAfterAsync(9, 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseManualBalanceMovementAsync(
            "E1", 22, new ReverseLeaveBalanceMovementDto("Duplicate correction"), "HR"));
        Assert.Contains("Reverse later balance movements", error.Message);
        repository.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManualMovementCannotBeReversedThroughAnotherEmployeesScope()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E2", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(22, It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveBalanceMovement
        {
            Id = 22, BalanceId = 9, EmployeeCode = "E1", LeaveTypeCode = "A", SequenceNo = 3,
            Kind = "CORRECTION", Source = "MANUAL:original"
        });

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReverseManualBalanceMovementAsync(
            "E2", 22, new ReverseLeaveBalanceMovementDto("Wrong employee"), "HR"));
        repository.Verify(r => r.HasMovementReversalAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, "MANUAL_GRANT", "valid reason")]
    [InlineData(-1, "MANUAL_GRANT", "valid reason")]
    [InlineData(100000000, "MANUAL_GRANT", "valid reason")]
    [InlineData(1, "UNKNOWN", "valid reason")]
    [InlineData(1, "CORRECTION", "   ")]
    public async Task ManualAdjustmentRejectsInvalidTypeSignZeroAndMissingReason(decimal days, string type, string? reason)
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveType { LeaveTypeCode = "A" });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        var service = new LeaveService(repository.Object, employeeRepo.Object);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GrantOrAdjustBalanceAsync(
            new GrantLeaveBalanceDto("E1", "A", 2026, days, type, reason, Guid.NewGuid().ToString()), "HR"));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApprovalRecordsUsageAndOnlyInitializesAccrualWhenBalanceIsAbsent(bool existingBalance)
    {
        var repository = new Mock<ILeaveRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var leaveType = new LeaveType { LeaveTypeCode = "A" };
        var request = new LeaveRequest { Id = 77, EmployeeCode = "E1", LeaveTypeCode = "A", LeaveType = leaveType,
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 1), DaysRequested = 1 };
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string e, Func<Task<LeaveRequestDto>> action, CancellationToken ct) => action());
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1", HireDate = new DateTime(2020, 1, 1) });
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBalance ? new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "A", YearNo = 2026, AccruedDays = 5, UsedDays = 1 } : null);
        var recorded = new List<LeaveBalanceMovement>();
        repository.Setup(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, LeaveBalanceState, string, string, string, string, string, string?, decimal, decimal, long?, CancellationToken>((b, before, kind, key, source, reason, actor, hash, expired, transfer, reverse, ct) =>
                recorded.Add(LeaveBalanceMovement.Create(b, before, kind, key, source, reason, actor, recorded.Count)))
            .Returns(Task.CompletedTask);
        var result = await new LeaveService(repository.Object, employees.Object).ProcessLeaveRequestAsync(77, new ProcessLeaveRequestDto(true, null), "HR");
        Assert.Equal("APPROVED", result.Status);
        Assert.Equal(existingBalance ? 1 : 2, recorded.Count);
        var usage = Assert.Single(recorded.Where(m => m.Kind == "USAGE"));
        Assert.Equal(1m, usage.UsedDelta);
        Assert.Equal(0m, usage.AccruedDelta);
        if (!existingBalance)
        {
            var accrual = Assert.Single(recorded.Where(m => m.Kind == "ACCRUAL"));
            Assert.True(accrual.AccruedDelta > 0);
            Assert.Equal(0m, accrual.UsedDelta);
        }
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovementReadRejectsAnEmployeeOutsideTheScope()
    {
        var repository = new Mock<ILeaveRepository>();
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMovementsAsync("outside", null, null, 1, 50));
        repository.Verify(r => r.GetMovementsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMovementsAsync_ValidEmployeeAndPagination_ReturnsMappedPage()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1" });

        var movements = new List<LeaveBalanceMovement>
        {
            new()
            {
                Id = 1,
                BalanceId = 10,
                EmployeeCode = "E1",
                LeaveTypeCode = "ANNUAL",
                CycleStartDate = new DateTime(2026, 1, 1),
                SequenceNo = 1,
                Kind = "ACCRUAL",
                Source = "MONTHLY_POLICY",
                Reason = "Monthly accrual",
                Actor = "SYSTEM",
                CreatedDate = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc),
                AccruedDelta = 1.75m,
                UsedDelta = 0m,
                CarriedDelta = 0m,
                AccruedAfter = 1.75m,
                UsedAfter = 0m,
                CarriedAfter = 0m
            }
        };

        repository.Setup(r => r.GetMovementsAsync("E1", "ANNUAL", null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((movements, 1));

        var service = new LeaveService(repository.Object, employeeRepo.Object);
        var result = await service.GetMovementsAsync("E1", "ANNUAL", null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(20, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.Id);
        Assert.Equal("ANNUAL", item.LeaveTypeCode);
        Assert.Equal(1.75m, item.AvailableAfter);
        Assert.Equal(1.75m, item.AccruedDelta);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1000001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task GetMovementsAsync_InvalidPagination_ThrowsArgumentException(int page, int size)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMovementsAsync("E1", null, null, page, size));
    }

    [Theory]
    [InlineData("USAGE", "MANUAL:correction")]
    [InlineData("YEAR_CLOSE_EXPIRY", "MANUAL:correction")]
    [InlineData("CORRECTION", "SYSTEM:auto")]
    [InlineData("ACCRUAL", "POLICY")]
    public async Task ReverseManualBalanceMovement_NonReversibleKindOrSource_ThrowsInvalidOperationException(string kind, string source)
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalanceMovement
            {
                Id = 50,
                BalanceId = 1,
                EmployeeCode = "E1",
                LeaveTypeCode = "A",
                Kind = kind,
                Source = source
            });

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseManualBalanceMovementAsync("E1", 50, new ReverseLeaveBalanceMovementDto("Reversal reason"), "HR"));

        Assert.Contains("Only a manual opening, grant, or correction movement can be reversed", ex.Message);
    }

    [Fact]
    public async Task ReverseManualBalanceMovement_BalanceChangedAfterAdjustment_ThrowsInvalidOperationException()
    {
        var repository = new Mock<ILeaveRepository>();
        var balance = new LeaveBalance
        {
            Id = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1),
            CycleEndDate = new DateTime(2026, 12, 31),
            AccruedDays = 10m, // Changed after adjustment (was 5)
            UsedDays = 0m,
            CarriedForwardDays = 0m
        };

        var movement = new LeaveBalanceMovement
        {
            Id = 60,
            BalanceId = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            CycleStartDate = new DateTime(2026, 1, 1),
            SequenceNo = 2,
            Kind = "CORRECTION",
            Source = "MANUAL:adj",
            AccruedDelta = 2m,
            AccruedAfter = 5m, // Expected 5m, but current balance is 10m
            UsedAfter = 0m,
            CarriedAfter = 0m
        };

        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(60, It.IsAny<CancellationToken>())).ReturnsAsync(movement);
        repository.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMovement?)null);
        repository.Setup(r => r.HasMovementReversalAsync(60, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.HasActiveMovementAfterAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>())).ReturnsAsync(balance);

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseManualBalanceMovementAsync("E1", 60, new ReverseLeaveBalanceMovementDto("Valid reason"), "HR"));

        Assert.Contains("The balance changed after this manual adjustment", ex.Message);
    }

    [Fact]
    public async Task ReverseManualBalanceMovement_CreatingNegativeBalance_ThrowsInvalidOperationException()
    {
        var repository = new Mock<ILeaveRepository>();
        var balance = new LeaveBalance
        {
            Id = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1),
            CycleEndDate = new DateTime(2026, 12, 31),
            AccruedDays = 2m, // Reversing a 5m grant would result in 2 - 5 = -3
            UsedDays = 0m,
            CarriedForwardDays = 0m
        };

        var movement = new LeaveBalanceMovement
        {
            Id = 61,
            BalanceId = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            CycleStartDate = new DateTime(2026, 1, 1),
            SequenceNo = 2,
            Kind = "MANUAL_GRANT",
            Source = "MANUAL:grant",
            AccruedDelta = 5m,
            AccruedAfter = 2m,
            UsedAfter = 0m,
            CarriedAfter = 0m
        };

        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(61, It.IsAny<CancellationToken>())).ReturnsAsync(movement);
        repository.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMovement?)null);
        repository.Setup(r => r.HasMovementReversalAsync(61, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.HasActiveMovementAfterAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>())).ReturnsAsync(balance);

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseManualBalanceMovementAsync("E1", 61, new ReverseLeaveBalanceMovementDto("Valid reason"), "HR"));

        Assert.Contains("negative leave balance", ex.Message);
    }

    [Fact]
    public async Task ReverseManualBalanceMovement_InvalidatingPendingRequests_ThrowsInvalidOperationException()
    {
        var repository = new Mock<ILeaveRepository>();
        var balance = new LeaveBalance
        {
            Id = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1),
            CycleEndDate = new DateTime(2026, 12, 31),
            AccruedDays = 5m,
            UsedDays = 0m,
            CarriedForwardDays = 0m
        };

        var movement = new LeaveBalanceMovement
        {
            Id = 62,
            BalanceId = 1,
            EmployeeCode = "E1",
            LeaveTypeCode = "A",
            CycleStartDate = new DateTime(2026, 1, 1),
            SequenceNo = 2,
            Kind = "MANUAL_GRANT",
            Source = "MANUAL:grant",
            AccruedDelta = 3m, // 5 - 3 = 2 available after reversal
            AccruedAfter = 5m,
            UsedAfter = 0m,
            CarriedAfter = 0m
        };

        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveBalanceDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetMovementByIdAsync(62, It.IsAny<CancellationToken>())).ReturnsAsync(movement);
        repository.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMovement?)null);
        repository.Setup(r => r.HasMovementReversalAsync(62, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.HasActiveMovementAfterAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "A", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.GetPendingLeaveDaysAsync("E1", "A", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4m); // 4 pending days > 2 available after reversal!

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseManualBalanceMovementAsync("E1", 62, new ReverseLeaveBalanceMovementDto("Valid reason"), "HR"));

        Assert.Contains("invalidate pending leave requests", ex.Message);
    }

    [Theory]
    [InlineData("", 1, "Valid reason")]
    [InlineData("E1", 0, "Valid reason")]
    [InlineData("E1", -1, "Valid reason")]
    [InlineData("E1", 1, "")]
    [InlineData("E1", 1, "   ")]
    public async Task ReverseManualBalanceMovement_InvalidArguments_ThrowsArgumentException(string empCode, long movId, string reason)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.ReverseManualBalanceMovementAsync(empCode, movId, new ReverseLeaveBalanceMovementDto(reason), "HR"));
    }
}
