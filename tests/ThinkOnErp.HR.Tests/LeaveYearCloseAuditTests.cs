using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeaveYearCloseAuditTests
{
    [Fact]
    public async Task DraftRejectsNegativeBalanceBeforeSaving()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 2m, UsedDays = 3m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true }
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(
            new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR"));

        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshingDraftRecalculatesCarryCapAndExcessActionFromCurrentPolicy()
    {
        var leaveType = new LeaveType
        {
            LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true,
            CarryForwardCapDays = 5m, YearEndExcessAction = "EXPIRE"
        };
        var balance = new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 10m, UsedDays = 2m, LeaveType = leaveType
        };
        var existing = new LeaveYearClose { Id = 21, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026, Status = "DRAFT" };
        var fixture = new Fixture(balance, existing);

        var first = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");
        Assert.Equal(5m, Assert.Single(first.Lines).CarriedDays);
        Assert.Equal(3m, first.TotalExpiredDays);
        var firstPolicySnapshot = Assert.Single(fixture.LastSavedClose!.Lines);
        Assert.True(firstPolicySnapshot.TracksBalanceSnapshot);
        Assert.True(firstPolicySnapshot.CarryForwardAllowedSnapshot);
        Assert.Equal(5m, firstPolicySnapshot.CarryForwardCapDaysSnapshot);
        Assert.Equal("EXPIRE", firstPolicySnapshot.YearEndExcessActionSnapshot);

        leaveType.CarryForwardCapDays = 2m;
        leaveType.YearEndExcessAction = "CASHOUT";
        var refreshed = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(refreshed.Lines);
        Assert.Equal(2m, line.CarriedDays);
        Assert.Equal(0m, line.ExpiredDays);
        Assert.Equal(6m, line.CashoutDays);
        var refreshedPolicySnapshot = Assert.Single(fixture.LastSavedClose!.Lines);
        Assert.Equal(2m, refreshedPolicySnapshot.CarryForwardCapDaysSnapshot);
        Assert.Equal("CASHOUT", refreshedPolicySnapshot.YearEndExcessActionSnapshot);
        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.Is<LeaveYearClose>(close => close.Id == 21), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RequireUseRejectsDraftWhenUnusedDaysExceedCarryCap()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType
            {
                LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true,
                CarryForwardCapDays = 5m, YearEndExcessAction = "REQUIRE_USE"
            }
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(
            new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR"));

        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryingPostedCloseReturnsExistingSnapshotWithoutRebuilding()
    {
        var posted = new LeaveYearClose
        {
            Id = 31, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026,
            LeaveTypeCode = "ANNUAL", Status = "POSTED", PostedBy = "APPROVER",
            Lines = [new LeaveYearCloseLine
            {
                EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", SourceYear = 2025, TargetYear = 2026,
                SourceCycleStartDate = new DateTime(2025, 1, 1), SourceCycleEndDate = new DateTime(2025, 12, 31),
                TargetCycleStartDate = new DateTime(2026, 1, 1), TargetCycleEndDate = new DateTime(2026, 12, 31),
                UnusedDays = 4m, CarriedDays = 4m
            }]
        };
        var fixture = new Fixture(new LeaveBalance(), posted);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        Assert.Equal("POSTED", result.Status);
        Assert.Equal(4m, Assert.Single(result.Lines).CarriedDays);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9999, 10000)]
    [InlineData(2025, 2027)]
    [InlineData(2025, 2025)]
    public async Task YearRolloverValidatesFromAndToYear(int fromYear, int toYear)
    {
        var fixture = new Fixture(new LeaveBalance());
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(
            new YearEndRolloverDto(fromYear, toYear, "ANNUAL"), "HR"));
    }

    [Fact]
    public async Task DraftRejectsWhenPendingLeaveRequestsExist()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5 }
        });

        fixture.Repository.Setup(r => r.HasPendingLeaveRequestsAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(
            new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR"));

        Assert.Contains("Resolve pending leave requests", ex.Message);
        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DraftSkipsLeaveTypesThatDoNotTrackBalance()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "UNPAID", YearNo = 2025,
            AccruedDays = 0m, UsedDays = 5m,
            LeaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false }
        });

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "UNPAID"), "HR");

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.TotalCarriedDays);
        Assert.Equal(0m, result.TotalExpiredDays);
    }

    [Fact]
    public async Task CarryForwardDisabledForcesAllUnusedDaysToExcess()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 15m, UsedDays = 5m,
            LeaveType = new LeaveType
            {
                LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = false,
                CarryForwardCapDays = 0m, YearEndExcessAction = "EXPIRE"
            }
        });

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        Assert.Equal(10m, line.UnusedDays);
        Assert.Equal(0m, line.CarriedDays);
        Assert.Equal(10m, line.ExpiredDays);
        Assert.Equal(0m, line.CashoutDays);
    }

    [Fact]
    public async Task ZeroUnusedDaysDraftsCleanlyWithoutExcess()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 10m, UsedDays = 10m,
            LeaveType = new LeaveType
            {
                LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true,
                CarryForwardCapDays = 5m, YearEndExcessAction = "EXPIRE"
            }
        });

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        Assert.Equal(0m, line.UnusedDays);
        Assert.Equal(0m, line.CarriedDays);
        Assert.Equal(0m, line.ExpiredDays);
        Assert.Equal(0m, line.CashoutDays);
    }

    [Fact]
    public async Task RunYearEndRolloverAsyncCreatesDraftAndPostsImmediately()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 12m, UsedDays = 2m,
            LeaveType = new LeaveType
            {
                LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true,
                CarryForwardCapDays = 5m, YearEndExcessAction = "EXPIRE"
            }
        });

        var postedClose = new LeaveYearClose
        {
            Id = 22, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026,
            LeaveTypeCode = "ANNUAL", Status = "POSTED", PostedBy = "HR",
            Lines = [new LeaveYearCloseLine
            {
                EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", SourceYear = 2025, TargetYear = 2026,
                UnusedDays = 10m, CarriedDays = 5m, ExpiredDays = 5m
            }]
        };

        fixture.Repository.Setup(r => r.PostLeaveYearCloseAsync(22, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(postedClose);

        var result = await fixture.Service.RunYearEndRolloverAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(1, result.RolloverBalancesCount);
        Assert.Equal(5m, result.TotalDaysCarriedForward);
        Assert.Equal(5m, result.TotalDaysExpired);
        Assert.Contains("E1 [ANNUAL]: Unused 10.0000 => carried 5.0000, expired 5.0000", result.Details);
        fixture.Repository.Verify(r => r.PostLeaveYearCloseAsync(22, "HR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostLeaveYearCloseAsyncThrowsKeyNotFoundWhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.PostLeaveYearCloseAsync(999, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PostLeaveYearCloseAsync(999, "HR"));
    }

    [Fact]
    public async Task UnpostLeaveYearCloseAsyncThrowsKeyNotFoundWhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.UnpostLeaveYearCloseAsync(999, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.UnpostLeaveYearCloseAsync(999, "HR"));
    }

    [Fact]
    public async Task GetLeaveYearCloseAsyncThrowsKeyNotFoundWhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetLeaveYearCloseAsync(999));
    }

    [Fact]
    public async Task PostAndUnpostLeaveYearCloseAsyncDelegateToRepositoryAndMapDto()
    {
        var fixture = new Fixture(new LeaveBalance());
        var close = new LeaveYearClose
        {
            Id = 55, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026,
            LeaveTypeCode = "ANNUAL", Status = "POSTED", PostedBy = "ADMIN",
            Lines = [new LeaveYearCloseLine
            {
                EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", SourceYear = 2025, TargetYear = 2026,
                UnusedDays = 8m, CarriedDays = 5m, ExpiredDays = 3m
            }]
        };

        fixture.Repository.Setup(r => r.PostLeaveYearCloseAsync(55, "ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(close);

        var postedDto = await fixture.Service.PostLeaveYearCloseAsync(55, "ADMIN");
        Assert.Equal("POSTED", postedDto.Status);
        Assert.Equal(5m, postedDto.TotalCarriedDays);

        close.Status = "UNPOSTED";
        fixture.Repository.Setup(r => r.UnpostLeaveYearCloseAsync(55, "ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(close);

        var unpostedDto = await fixture.Service.UnpostLeaveYearCloseAsync(55, "ADMIN");
        Assert.Equal("UNPOSTED", unpostedDto.Status);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var fixture = new Fixture(new LeaveBalance());
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(null!, "HR"));
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenLeaveTypeCodeWhitespace_NormalizesToWildcardAsterisk()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5 }
        });

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "   "), "HR");

        Assert.Equal("*", result.LeaveTypeCode);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(2025, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenLeaveTypeCodeMixedCase_NormalizesToUppercaseTrimmed()
    {
        var fixture = new Fixture(new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5 }
        });

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "  annual  "), "HR");

        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(2025, "ANNUAL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_MultipleEmployees_CalculatesTotalsAndOrdersDeterministically()
    {
        var ltAnnual = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m, YearEndExcessAction = "EXPIRE" };
        var ltCasual = new LeaveType { LeaveTypeCode = "CASUAL", TracksBalance = true, CarryForwardAllowed = false, YearEndExcessAction = "CASHOUT" };

        var b1 = new LeaveBalance { Id = 10, EmployeeCode = "E2", LeaveTypeCode = "ANNUAL", YearNo = 2025, AccruedDays = 10m, UsedDays = 2m, LeaveType = ltAnnual };
        var b2 = new LeaveBalance { Id = 20, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025, AccruedDays = 8m, UsedDays = 3m, LeaveType = ltAnnual };
        var b3 = new LeaveBalance { Id = 30, EmployeeCode = "E1", LeaveTypeCode = "CASUAL", YearNo = 2025, AccruedDays = 6m, UsedDays = 1m, LeaveType = ltCasual };

        var fixture = new Fixture(new[] { b1, b2, b3 });
        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "*"), "HR");

        Assert.Equal(3, result.Lines.Count);
        // Ordering: EmployeeCode, then LeaveTypeCode, then Id -> E1/ANNUAL, E1/CASUAL, E2/ANNUAL
        Assert.Equal("E1", result.Lines[0].EmployeeCode);
        Assert.Equal("ANNUAL", result.Lines[0].LeaveTypeCode);
        Assert.Equal("E1", result.Lines[1].EmployeeCode);
        Assert.Equal("CASUAL", result.Lines[1].LeaveTypeCode);
        Assert.Equal("E2", result.Lines[2].EmployeeCode);
        Assert.Equal("ANNUAL", result.Lines[2].LeaveTypeCode);

        // E1/ANNUAL: Unused = 5, Carried = 5, Expired = 0, Cashout = 0
        Assert.Equal(5m, result.Lines[0].CarriedDays);
        Assert.Equal(0m, result.Lines[0].ExpiredDays);

        // E1/CASUAL: Unused = 5, Carried = 0, Expired = 0, Cashout = 5
        Assert.Equal(0m, result.Lines[1].CarriedDays);
        Assert.Equal(5m, result.Lines[1].CashoutDays);

        // E2/ANNUAL: Unused = 8, Carried = 5, Expired = 3, Cashout = 0
        Assert.Equal(5m, result.Lines[2].CarriedDays);
        Assert.Equal(3m, result.Lines[2].ExpiredDays);

        // Totals: Carried = 5+0+5 = 10, Expired = 0+0+3 = 3, Cashout = 0+5+0 = 5, Unused = 5+5+8 = 18
        Assert.Equal(10m, result.TotalCarriedDays);
        Assert.Equal(3m, result.TotalExpiredDays);
        Assert.Equal(5m, result.TotalCashoutDays);
        Assert.Equal(18m, result.TotalUnusedDays);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_PreviousCarriedDaysIncludedInUnusedBalanceCalculation()
    {
        var lt = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 6m, YearEndExcessAction = "EXPIRE" };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, CarriedForwardDays = 5m, UsedDays = 3m,
            LeaveType = lt
        };

        var fixture = new Fixture(balance);
        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        // Unused = 10 + 5 - 3 = 12
        Assert.Equal(12m, line.UnusedDays);
        Assert.Equal(6m, line.CarriedDays);
        Assert.Equal(6m, line.ExpiredDays);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_UnlimitedCarryover_WhenCarryForwardCapDaysZeroOrNegative()
    {
        var lt = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 0m, YearEndExcessAction = "EXPIRE" };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 15m, UsedDays = 3m,
            LeaveType = lt
        };

        var fixture = new Fixture(balance);
        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        // Unused = 12, Cap <= 0 => carry all 12
        Assert.Equal(12m, line.UnusedDays);
        Assert.Equal(12m, line.CarriedDays);
        Assert.Equal(0m, line.ExpiredDays);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_ExistingTargetBalance_PopulatesTargetAccruedAndCarriedBefore()
    {
        var lt = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 1, 1), CycleEndDate = new DateTime(2025, 12, 31),
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = lt
        };

        var targetBalance = new LeaveBalance
        {
            Id = 99, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026,
            CycleStartDate = new DateTime(2026, 1, 1), CycleEndDate = new DateTime(2026, 12, 31),
            AccruedDays = 14m, CarriedForwardDays = 2m, UsedDays = 1m
        };

        var fixture = new Fixture(balance);
        fixture.Repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "ANNUAL", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetBalance);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        Assert.Single(result.Lines);
        var savedLine = Assert.Single(fixture.LastSavedClose!.Lines);
        Assert.Equal(99, savedLine.TargetBalanceId);
        Assert.Equal(14m, savedLine.TargetAccruedBefore);
        Assert.Equal(2m, savedLine.TargetCarriedBefore);
        Assert.Equal(1m, savedLine.TargetUsedBefore);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenLeaveTypeNullOnEntity_LoadsFromRepositoryByCode()
    {
        var lt = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 4m };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = null // not populated on navigation property
        };

        var fixture = new Fixture(balance);
        fixture.Repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lt);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        Assert.Equal(4m, line.CarriedDays);
        Assert.Equal(4m, line.ExpiredDays);
        fixture.Repository.Verify(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenLeaveTypeNotFoundInRepository_SkipsCleanly()
    {
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "UNKNOWN", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = null
        };

        var fixture = new Fixture(balance);
        fixture.Repository.Setup(r => r.GetLeaveTypeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "UNKNOWN"), "HR");

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_CustomCycleDates_CalculatesTargetCycleDatesCorrectly()
    {
        var lt = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            CycleStartDate = new DateTime(2025, 4, 1), CycleEndDate = new DateTime(2026, 3, 31),
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = lt
        };

        var fixture = new Fixture(balance);
        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        var line = Assert.Single(result.Lines);
        Assert.Equal(new DateTime(2025, 4, 1), line.SourceCycleStartDate);
        Assert.Equal(new DateTime(2026, 3, 31), line.SourceCycleEndDate);
        Assert.Equal(new DateTime(2026, 4, 1), line.TargetCycleStartDate);
        Assert.Equal(new DateTime(2027, 3, 31), line.TargetCycleEndDate);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_ExistingDraft_PreservesIdCreationUserAndCreationDate()
    {
        var existing = new LeaveYearClose
        {
            Id = 77, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026,
            LeaveTypeCode = "ANNUAL", Status = "DRAFT",
            CreationUser = "FOUNDING_USER", CreationDate = new DateTime(2025, 12, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5 }
        };

        var fixture = new Fixture(balance, existing);
        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "UPDATING_USER");

        Assert.Equal(77, result.Id);
        Assert.Equal("FOUNDING_USER", result.CreationUser);
        Assert.Equal(new DateTime(2025, 12, 1, 10, 0, 0, DateTimeKind.Utc), result.CreationDate);
    }

    [Fact]
    public async Task RunYearEndRolloverAsync_WhenDraftAlreadyPosted_DoesNotCallPostAgain()
    {
        var postedClose = new LeaveYearClose
        {
            Id = 88, CloseKey = "2025:2026:ANNUAL", FromYear = 2025, ToYear = 2026,
            LeaveTypeCode = "ANNUAL", Status = "POSTED", PostedBy = "EARLIER_ADMIN",
            Lines = [new LeaveYearCloseLine
            {
                EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", SourceYear = 2025, TargetYear = 2026,
                UnusedDays = 10m, CarriedDays = 5m, ExpiredDays = 5m
            }]
        };

        var fixture = new Fixture(new LeaveBalance(), postedClose);
        var result = await fixture.Service.RunYearEndRolloverAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(5m, result.TotalDaysCarriedForward);
        Assert.Equal(5m, result.TotalDaysExpired);
        fixture.Repository.Verify(r => r.PostLeaveYearCloseAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        var fixture = new Fixture(new LeaveBalance());
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.CreateLeaveYearCloseDraftAsync(null!, "USER"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(9999, 10000)]
    [InlineData(2025, 2025)]
    [InlineData(2025, 2027)]
    public async Task CreateLeaveYearCloseDraftAsync_ThrowsArgumentException_WhenYearsAreInvalid(int fromYear, int toYear)
    {
        var fixture = new Fixture(new LeaveBalance());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(fromYear, toYear, "ANNUAL"), "USER"));
        Assert.Contains("Leave-year close must move to the immediately following cycle year.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateLeaveYearCloseDraftAsync_WildcardLeaveTypeCode_WhenNullOrWhitespace(string? leaveTypeCode)
    {
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m }
        };
        var fixture = new Fixture(balance);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, leaveTypeCode), "HR");

        Assert.Equal("*", result.LeaveTypeCode);
        Assert.Equal("2025:2026:*", fixture.LastSavedClose?.CloseKey);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(2025, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_TrimsAndUpperCasesLeaveTypeCode()
    {
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m }
        };
        var fixture = new Fixture(balance);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "  annual  "), "HR");

        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        Assert.Equal("2025:2026:ANNUAL", fixture.LastSavedClose?.CloseKey);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(2025, "ANNUAL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, CarryForwardAllowed = true, CarryForwardCapDays = 5m }
        };
        var fixture = new Fixture(balance);

        await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR", cts.Token);

        fixture.Repository.Verify(r => r.GetLeaveYearCloseByKeyAsync("2025:2026:ANNUAL", cts.Token), Times.Once);
        fixture.Repository.Verify(r => r.GetLeaveBalancesByYearAsync(2025, "ANNUAL", cts.Token), Times.Once);
        fixture.Repository.Verify(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.GetLeaveYearCloseByKeyAsync(It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "ANNUAL"), "HR", cts.Token));
    }

    [Fact]
    public async Task GetLeaveYearCloseAsync_ThrowsKeyNotFoundException_WhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetLeaveYearCloseAsync(999));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task GetLeaveYearCloseAsync_ReturnsMappedDto_WhenFound()
    {
        var close = new LeaveYearClose
        {
            Id = 42,
            FromYear = 2025,
            ToYear = 2026,
            LeaveTypeCode = "ANNUAL",
            Status = "DRAFT",
            CreationUser = "HR_ADMIN",
            CreationDate = new DateTime(2025, 12, 31, 10, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new LeaveYearCloseLine
                {
                    EmployeeCode = "EMP01",
                    LeaveTypeCode = "ANNUAL",
                    SourceYear = 2025,
                    TargetYear = 2026,
                    UnusedDays = 8m,
                    CarriedDays = 5m,
                    ExpiredDays = 3m,
                    CashoutDays = 0m
                }
            ]
        };

        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(close);

        var dto = await fixture.Service.GetLeaveYearCloseAsync(42);

        Assert.Equal(42, dto.Id);
        Assert.Equal(2025, dto.FromYear);
        Assert.Equal(2026, dto.ToYear);
        Assert.Equal("ANNUAL", dto.LeaveTypeCode);
        Assert.Equal("DRAFT", dto.Status);
        Assert.Equal(8m, dto.TotalUnusedDays);
        Assert.Equal(5m, dto.TotalCarriedDays);
        Assert.Equal(3m, dto.TotalExpiredDays);
        var line = Assert.Single(dto.Lines);
        Assert.Equal("EMP01", line.EmployeeCode);
    }

    [Fact]
    public async Task GetLeaveYearCloseAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var close = new LeaveYearClose { Id = 10, Status = "DRAFT", Lines = [] };
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.GetLeaveYearCloseAsync(10, cts.Token))
            .ReturnsAsync(close);

        var dto = await fixture.Service.GetLeaveYearCloseAsync(10, cts.Token);
        Assert.Equal(10, dto.Id);
        fixture.Repository.Verify(r => r.GetLeaveYearCloseAsync(10, cts.Token), Times.Once);
    }

    [Fact]
    public async Task PostLeaveYearCloseAsync_ThrowsKeyNotFoundException_WhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.PostLeaveYearCloseAsync(999, "APPROVER", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PostLeaveYearCloseAsync(999, "APPROVER"));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task PostLeaveYearCloseAsync_ReturnsMappedDto_AndPropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var close = new LeaveYearClose
        {
            Id = 55, Status = "POSTED", PostedBy = "APPROVER", PostedDate = DateTime.UtcNow, Lines = []
        };
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.PostLeaveYearCloseAsync(55, "APPROVER", cts.Token))
            .ReturnsAsync(close);

        var result = await fixture.Service.PostLeaveYearCloseAsync(55, "APPROVER", cts.Token);

        Assert.Equal("POSTED", result.Status);
        Assert.Equal("APPROVER", result.PostedBy);
        fixture.Repository.Verify(r => r.PostLeaveYearCloseAsync(55, "APPROVER", cts.Token), Times.Once);
    }

    [Fact]
    public async Task UnpostLeaveYearCloseAsync_ThrowsKeyNotFoundException_WhenNotFound()
    {
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.UnpostLeaveYearCloseAsync(888, "ADMIN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveYearClose?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.UnpostLeaveYearCloseAsync(888, "ADMIN"));
        Assert.Contains("888", ex.Message);
    }

    [Fact]
    public async Task UnpostLeaveYearCloseAsync_ReturnsMappedDto_AndPropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var close = new LeaveYearClose
        {
            Id = 88, Status = "DRAFT", UnpostedBy = "ADMIN", UnpostedDate = DateTime.UtcNow, Lines = []
        };
        var fixture = new Fixture(new LeaveBalance());
        fixture.Repository.Setup(r => r.UnpostLeaveYearCloseAsync(88, "ADMIN", cts.Token))
            .ReturnsAsync(close);

        var result = await fixture.Service.UnpostLeaveYearCloseAsync(88, "ADMIN", cts.Token);

        Assert.Equal("DRAFT", result.Status);
        Assert.Equal("ADMIN", result.UnpostedBy);
        fixture.Repository.Verify(r => r.UnpostLeaveYearCloseAsync(88, "ADMIN", cts.Token), Times.Once);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_IgnoresBalances_WhenLeaveTypeDoesNotTrackBalance()
    {
        var nonTrackingLt = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "UNPAID", YearNo = 2025,
            AccruedDays = 10m, UsedDays = 2m,
            LeaveType = nonTrackingLt
        };
        var fixture = new Fixture(balance);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "UNPAID"), "HR");

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task CreateLeaveYearCloseDraftAsync_WhenLeaveTypeNullInBalance_FetchesFromRepository()
    {
        var lt = new LeaveType { LeaveTypeCode = "SICK", TracksBalance = true, CarryForwardAllowed = false, YearEndExcessAction = "EXPIRE" };
        var balance = new LeaveBalance
        {
            Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "SICK", YearNo = 2025,
            AccruedDays = 15m, UsedDays = 5m,
            LeaveType = null
        };
        var fixture = new Fixture(balance);
        fixture.Repository.Setup(r => r.GetLeaveTypeByCodeAsync("SICK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lt);

        var result = await fixture.Service.CreateLeaveYearCloseDraftAsync(new YearEndRolloverDto(2025, 2026, "SICK"), "HR");

        var line = Assert.Single(result.Lines);
        Assert.Equal("SICK", line.LeaveTypeCode);
        Assert.Equal(10m, line.UnusedDays);
        Assert.Equal(0m, line.CarriedDays);
        Assert.Equal(10m, line.ExpiredDays);
        fixture.Repository.Verify(r => r.GetLeaveTypeByCodeAsync("SICK", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture
    {
        public Mock<ILeaveRepository> Repository { get; } = new();
        public ILeaveService Service { get; }
        public LeaveYearClose? LastSavedClose { get; private set; }

        public Fixture(LeaveBalance balance, LeaveYearClose? existing = null)
            : this(new[] { balance }, existing) { }

        public Fixture(IEnumerable<LeaveBalance> balances, LeaveYearClose? existing = null)
        {
            var list = balances.ToList();
            Repository.Setup(r => r.GetLeaveYearCloseByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            Repository.Setup(r => r.GetLeaveBalancesByYearAsync(2025, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            Repository.Setup(r => r.HasPendingLeaveRequestsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            Repository.Setup(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LeaveBalance?)null);
            Repository.Setup(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LeaveYearClose close, CancellationToken _) =>
                {
                    close.Id = existing?.Id ?? 22;
                    LastSavedClose = close;
                    return close;
                });
            Service = new LeaveService(Repository.Object, Mock.Of<IEmployeeRepository>());
        }
    }
}
