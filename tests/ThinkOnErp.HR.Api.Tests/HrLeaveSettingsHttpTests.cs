using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed partial class HrAttendanceHttpTests
{
    [Fact]
    public async Task LeaveBalanceHttp_ReportsPendingAndAvailableInsideTenantCycle()
    {
        var today = DateTime.UtcNow.Date;
        var cycleStart = new DateTime(today.Month >= 7 ? today.Year : today.Year - 1, 7, 1);
        var cycleEnd = cycleStart.AddYears(1).AddDays(-1);
        using (var storeOne = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            storeOne.AddRange(
                new ThinkOnErp.Domain.Entities.Hr.HrLeaveSettings { Id = 1, LeaveYearStartMonth = 7, LeaveYearStartDay = 1 },
                new ThinkOnErp.Domain.Entities.Hr.LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true, MaxDaysPerYear = 15m },
                new ThinkOnErp.Domain.Entities.Hr.LeaveBalance { Id = 501, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL", YearNo = cycleStart.Year,
                    CycleStartDate = cycleStart, CycleEndDate = cycleEnd, AccruedDays = 15m, UsedDays = 3m, CarriedForwardDays = 2m },
                new ThinkOnErp.Domain.Entities.Hr.LeaveRequest { Id = 601, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL",
                    StartDate = cycleStart.AddDays(1), EndDate = cycleStart.AddDays(1), DaysRequested = 1.25m, Status = "PENDING" },
                new ThinkOnErp.Domain.Entities.Hr.LeaveRequest { Id = 602, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL",
                    StartDate = cycleStart.AddDays(3), EndDate = cycleStart.AddDays(4), DaysRequested = 2.5m, Status = "PENDING" },
                new ThinkOnErp.Domain.Entities.Hr.LeaveRequest { Id = 603, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL",
                    StartDate = cycleStart.AddDays(5), EndDate = cycleStart.AddDays(5), DaysRequested = 9m, Status = "REJECTED" },
                new ThinkOnErp.Domain.Entities.Hr.LeaveRequest { Id = 604, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL",
                    StartDate = cycleEnd.AddDays(1), EndDate = cycleEnd.AddDays(1), DaysRequested = 7m, Status = "PENDING" });
            await storeOne.SaveChangesAsync();
        }

        using (var companyOne = await Send("GET", "/api/hr/leaves/balances/SELF"))
        {
            Assert.Equal(HttpStatusCode.OK, companyOne.StatusCode);
            var balance = Assert.Single((await Data(companyOne)).EnumerateArray());
            Assert.Equal(3.75m, balance.GetProperty("pendingDays").GetDecimal());
            Assert.Equal(10.25m, balance.GetProperty("availableDays").GetDecimal());
            Assert.Equal(14m, balance.GetProperty("remainingDays").GetDecimal());
            Assert.Equal(cycleStart.ToString("yyyy-MM-dd"), balance.GetProperty("cycleStartDate").GetDateTime().ToString("yyyy-MM-dd"));
        }

        using (var companyTwo = await Send("GET", "/api/hr/leaves/balances/SELF", company: 2))
        {
            Assert.Equal(HttpStatusCode.OK, companyTwo.StatusCode);
            Assert.Empty((await Data(companyTwo)).EnumerateArray());
        }
    }

    [Fact]
    public async Task LeaveAccrualSettings_AreAuthorizedTenantScopedAndPreservedForOlderClients()
    {
        using (var anonymous = await Send("GET", "/api/hr/leaves/settings", company: null))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using (var initial = await Send("GET", "/api/hr/leaves/settings"))
        {
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
            var data = await Data(initial);
            Assert.False(data.GetProperty("automaticAccrualEnabled").GetBoolean());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("accrualTimeZoneId").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("eligibleCitizenshipCountryCode").ValueKind);
        }

        using (var update = await Send("PUT", "/api/hr/leaves/settings",
            new UpdateHrLeaveSettingsDto(7, 1, "DECIMAL", null, true, "UTC", "jo")))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Old clients omit the new scheduler fields; an unrelated settings save must preserve them.
        using (var legacyUpdate = await Send("PUT", "/api/hr/leaves/settings",
            new UpdateHrLeaveSettingsDto(7, 1, "DECIMAL", null)))
            Assert.Equal(HttpStatusCode.OK, legacyUpdate.StatusCode);

        using (var companyOne = await Send("GET", "/api/hr/leaves/settings"))
        {
            Assert.Equal(HttpStatusCode.OK, companyOne.StatusCode);
            var data = await Data(companyOne);
            Assert.True(data.GetProperty("automaticAccrualEnabled").GetBoolean());
            Assert.Equal("UTC", data.GetProperty("accrualTimeZoneId").GetString());
            Assert.Equal("JO", data.GetProperty("eligibleCitizenshipCountryCode").GetString());
        }

        using var storeTwo = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(2));
        storeTwo.Remove(await storeTwo.SysUserScreenPermissions.SingleAsync(p => p.ScreenId == 53 && p.FeatureId == 63));
        await storeTwo.SaveChangesAsync();
        using (var forbidden = await Send("PUT", "/api/hr/leaves/settings",
            new UpdateHrLeaveSettingsDto(7, 1, "DECIMAL", null, true, "UTC", "JO"), company: 2))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using (var invalidCountry = await Send("PUT", "/api/hr/leaves/settings",
            new UpdateHrLeaveSettingsDto(7, 1, "DECIMAL", null, EligibleCitizenshipCountryCode: "J")))
            Assert.Equal(HttpStatusCode.BadRequest, invalidCountry.StatusCode);

        using (var companyTwo = await Send("GET", "/api/hr/leaves/settings", company: 2))
        {
            Assert.Equal(HttpStatusCode.OK, companyTwo.StatusCode);
            var data = await Data(companyTwo);
            Assert.False(data.GetProperty("automaticAccrualEnabled").GetBoolean());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("accrualTimeZoneId").ValueKind);
        }

        using var storeOne = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1));
        var savedOne = Assert.Single(await storeOne.HrLeaveSettings.ToListAsync());
        Assert.True(savedOne.AutomaticAccrualEnabled);
        Assert.Equal("UTC", savedOne.AccrualTimeZoneId);
        Assert.Equal("JO", savedOne.EligibleCitizenshipCountryCode);
        Assert.Empty(await storeTwo.HrLeaveSettings.ToListAsync());
    }

    [Fact]
    public async Task LeaveRequestHttp_SubmitThenCancelPending_ReleasesReservationAndEnforcesSelfScope()
    {
        var today = DateTime.UtcNow.Date;
        var cycleStart = new DateTime(today.Year, 1, 1);
        using (var store = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            store.AddRange(
                new ThinkOnErp.Domain.Entities.Hr.LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true, MaxDaysPerYear = 10m },
                new ThinkOnErp.Domain.Entities.Hr.LeaveBalance { Id = 701, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL", YearNo = today.Year,
                    CycleStartDate = cycleStart, CycleEndDate = cycleStart.AddYears(1).AddDays(-1), AccruedDays = 8m });
            await store.SaveChangesAsync();
        }

        var requestDate = today.AddDays(1);
        while (requestDate.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            requestDate = requestDate.AddDays(1);
        using var submitted = await Send("POST", "/api/hr/leaves/requests",
            new SubmitLeaveRequestDto("SELF", "ANNUAL", requestDate, requestDate, 2m, "family", null));
        Assert.True(submitted.StatusCode == HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var submittedData = await Data(submitted);
        Assert.Equal("PENDING", submittedData.GetProperty("status").GetString());
        Assert.Equal(1m, submittedData.GetProperty("daysRequested").GetDecimal());
        var requestId = submittedData.GetProperty("id").GetInt64();

        using (var wrongEmployee = await Send("POST", $"/api/hr/leaves/requests/OTHER/{requestId}/cancel", new CancelLeaveRequestDto("cancel")))
            Assert.Equal(HttpStatusCode.Forbidden, wrongEmployee.StatusCode);

        using var cancelled = await Send("POST", $"/api/hr/leaves/requests/SELF/{requestId}/cancel", new CancelLeaveRequestDto("Employee changed plans"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledData = await Data(cancelled);
        Assert.Equal("CANCELLED", cancelledData.GetProperty("status").GetString());
        Assert.Equal("Employee changed plans", cancelledData.GetProperty("cancellationReason").GetString());

        using var balanceResponse = await Send("GET", "/api/hr/leaves/balances/SELF");
        var balance = Assert.Single((await Data(balanceResponse)).EnumerateArray());
        Assert.Equal(0m, balance.GetProperty("pendingDays").GetDecimal());
        using var storeCheck = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1));
        Assert.Equal("CANCELLED", (await storeCheck.LeaveRequests.SingleAsync(r => r.Id == requestId)).Status);
        Assert.Empty(await storeCheck.Set<ThinkOnErp.Domain.Entities.Hr.LeaveBalanceMovement>().ToListAsync());
    }

    [Fact]
    public async Task LeaveRequestHttp_ApprovedCancellationReversesUsageExactlyOnce()
    {
        var today = DateTime.UtcNow.Date;
        var cycleStart = new DateTime(today.Year, 1, 1);
        using (var store = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            store.AddRange(
                new ThinkOnErp.Domain.Entities.Hr.LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true, MaxDaysPerYear = 10m },
                new ThinkOnErp.Domain.Entities.Hr.LeaveBalance { Id = 702, EmployeeCode = "SELF", LeaveTypeCode = "ANNUAL", YearNo = today.Year,
                    CycleStartDate = cycleStart, CycleEndDate = cycleStart.AddYears(1).AddDays(-1), AccruedDays = 8m });
            await store.SaveChangesAsync();
        }

        var requestDate = today.AddDays(1);
        while (requestDate.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            requestDate = requestDate.AddDays(1);
        using var submitted = await Send("POST", "/api/hr/leaves/requests",
            new SubmitLeaveRequestDto("SELF", "ANNUAL", requestDate, requestDate, 2m, null, null));
        Assert.True(submitted.StatusCode == HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var requestId = (await Data(submitted)).GetProperty("id").GetInt64();

        using var approved = await Send("POST", $"/api/hr/leaves/requests/{requestId}/process", new ProcessLeaveRequestDto(true, null));
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        using (var afterApproval = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
            Assert.Equal(1m, (await afterApproval.LeaveBalances.SingleAsync(b => b.Id == 702)).UsedDays);

        using var cancelled = await Send("POST", $"/api/hr/leaves/requests/SELF/{requestId}/cancel", new CancelLeaveRequestDto("Approved absence withdrawn"));
        Assert.True(cancelled.StatusCode == HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
        using var duplicate = await Send("POST", $"/api/hr/leaves/requests/SELF/{requestId}/cancel", new CancelLeaveRequestDto("Duplicate attempt"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        using var finalStore = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1));
        Assert.Equal(0m, (await finalStore.LeaveBalances.SingleAsync(b => b.Id == 702)).UsedDays);
        var movements = await finalStore.Set<ThinkOnErp.Domain.Entities.Hr.LeaveBalanceMovement>().OrderBy(m => m.SequenceNo).ToListAsync();
        var opening = Assert.Single(movements.Where(m => m.Kind == "OPENING_SNAPSHOT"));
        var usage = Assert.Single(movements.Where(m => m.Kind == "USAGE"));
        var reversal = Assert.Single(movements.Where(m => m.Kind == "REVERSAL"));
        Assert.Equal(0, opening.SequenceNo);
        Assert.Equal(1, usage.SequenceNo);
        Assert.Equal(2, reversal.SequenceNo);
        Assert.Equal(2m, usage.UsedDelta);
        Assert.Equal(-2m, reversal.UsedDelta);
        Assert.Equal(usage.Id, reversal.ReversesMovementId);
        Assert.Equal("CANCELLED", (await finalStore.LeaveRequests.SingleAsync(r => r.Id == requestId)).Status);
    }

    [Fact]
    public async Task ManualLeaveBalanceHttp_RequiresEditGrantValidatesBodyAndBindsEmployeeMovement()
    {
        var today = DateTime.UtcNow.Date;
        using (var setup = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            setup.Add(new ThinkOnErp.Domain.Entities.Hr.Employee { EmployeeCode = "SAME_BRANCH", BranchId = 7, HireDate = new DateTime(2020, 1, 1) });
            var edit = await setup.SysUserScreenPermissions.SingleAsync(p => p.UserId == 12 && p.ScreenId == 53 && p.FeatureId == 71 && p.BranchId == 7);
            setup.Remove(edit);
            await setup.SaveChangesAsync();
        }

        var grantDto = new GrantLeaveBalanceDto("SELF", "ANNUAL", today.Year, 2m, "COMPENSATORY", "Approved compensatory grant", "http-comp-grant-01");
        using (var deniedGrant = await Send("POST", "/api/hr/leaves/balances/grant", grantDto))
            Assert.Equal(HttpStatusCode.Forbidden, deniedGrant.StatusCode);
        using (var deniedReverse = await Send("POST", "/api/hr/leaves/balances/SELF/movements/999/reverse", new ReverseLeaveBalanceMovementDto("Denied")))
            Assert.Equal(HttpStatusCode.Forbidden, deniedReverse.StatusCode);

        using (var setup = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            setup.Add(new ThinkOnErp.Domain.Entities.SysUserScreenPermission { Id = 9971, UserId = 12, ScreenId = 53, FeatureId = 71, BranchId = 7, IsGranted = true });
            await setup.SaveChangesAsync();
        }

        var service = new Mock<ILeaveService>();
        var expected = new LeaveBalanceDto("SELF", "ANNUAL", "Annual", today.Year, 7m, 0m, 0m, 7m);
        service.Setup(s => s.GrantOrAdjustBalanceAsync(It.IsAny<GrantLeaveBalanceDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        service.Setup(s => s.ReverseManualBalanceMovementAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<ReverseLeaveBalanceMovementDto>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string employee, long _, ReverseLeaveBalanceMovementDto _, string _, CancellationToken _) => employee == "SELF"
                ? Task.FromResult(expected)
                : Task.FromException<LeaveBalanceDto>(new KeyNotFoundException("Movement not found for employee.")));
        leaveServiceOverride = service.Object;

        using var grant = await Send("POST", "/api/hr/leaves/balances/grant", grantDto);
        Assert.True(grant.StatusCode == HttpStatusCode.OK, await grant.Content.ReadAsStringAsync());
        service.Verify(s => s.GrantOrAdjustBalanceAsync(
            It.Is<GrantLeaveBalanceDto>(d => d.EmployeeCode == "SELF" && d.AdjustmentType == "COMPENSATORY" &&
                d.Reason == "Approved compensatory grant" && d.IdempotencyKey == "http-comp-grant-01"),
            "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using (var missingReason = await Send("POST", "/api/hr/leaves/balances/SELF/movements/990/reverse", new { }))
            Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using (var wrongEmployee = await Send("POST", "/api/hr/leaves/balances/SAME_BRANCH/movements/990/reverse",
            new ReverseLeaveBalanceMovementDto("Cross-employee attempt")))
            Assert.Equal(HttpStatusCode.NotFound, wrongEmployee.StatusCode);

        var reversalDto = new ReverseLeaveBalanceMovementDto("Compensatory hours were entered twice");
        using (var reversed = await Send("POST", "/api/hr/leaves/balances/SELF/movements/990/reverse", reversalDto))
            Assert.True(reversed.StatusCode == HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync());
        service.Verify(s => s.ReverseManualBalanceMovementAsync("SELF", 990,
            It.Is<ReverseLeaveBalanceMovementDto>(d => d.Reason == "Compensatory hours were entered twice"),
            "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.ReverseManualBalanceMovementAsync(It.IsAny<string>(), It.IsAny<long>(),
            It.IsAny<ReverseLeaveBalanceMovementDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task LeaveCashoutReverseHttp_RequiresAdminAndReasonAndForwardsSettlementId()
    {
        var reason = new ReverseLeaveCashoutSettlementDto("Correction after payroll reversal");
        using (var nonAdmin = await Send("POST", "/api/hr/leaves/balances/cashout/42/reverse", reason, adminClaim: true))
            Assert.Equal(HttpStatusCode.Forbidden, nonAdmin.StatusCode);

        using (var setup = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            (await setup.SysUsers.SingleAsync(user => user.Id == 12)).IsAdmin = true;
            await setup.SaveChangesAsync();
        }

        var service = new Mock<ILeaveService>();
        service.Setup(s => s.ReverseLeaveCashoutSettlementAsync(42, It.IsAny<ReverseLeaveCashoutSettlementDto>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveCashoutSettlementDto(42, "cashout-42", 7, "2026-09", "JOD", "BASIC", null, 26m,
                "LEAVE_CASHOUT", "REVERSED", "CREATOR", DateTime.UtcNow, "MOBILE_USER", DateTime.UtcNow,
                "MOBILE_USER", DateTime.UtcNow, reason.Reason, 1m, 100m, Array.Empty<LeaveCashoutSettlementLineDto>()));
        leaveServiceOverride = service.Object;

        using (var missingReason = await Send("POST", "/api/hr/leaves/balances/cashout/42/reverse", new { }, adminClaim: true))
            Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using var reversed = await Send("POST", "/api/hr/leaves/balances/cashout/42/reverse", reason, adminClaim: true);
        Assert.True(reversed.StatusCode == HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync());
        service.Verify(s => s.ReverseLeaveCashoutSettlementAsync(42,
            It.Is<ReverseLeaveCashoutSettlementDto>(dto => dto.Reason == "Correction after payroll reversal"),
            "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveCashoutDraftAndPostHttp_RequireAdminAndForwardSettlementInputs()
    {
        var draftRequest = new CreateLeaveCashoutSettlementDto("cashout-http-2026", 7, "2026-09", "JOD");
        using (var draftDenied = await Send("POST", "/api/hr/leaves/balances/cashout/draft", draftRequest, adminClaim: true))
            Assert.Equal(HttpStatusCode.Forbidden, draftDenied.StatusCode);
        using (var postDenied = await Send("POST", "/api/hr/leaves/balances/cashout/42/post", adminClaim: true))
            Assert.Equal(HttpStatusCode.Forbidden, postDenied.StatusCode);

        using (var setup = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            (await setup.SysUsers.SingleAsync(user => user.Id == 12)).IsAdmin = true;
            await setup.SaveChangesAsync();
        }

        var draft = new LeaveCashoutSettlementDto(42, draftRequest.SettlementKey, 7, "2026-09", "JOD", "BASIC", null,
            26m, "LEAVE_CASHOUT", "DRAFT", "MOBILE_USER", DateTime.UtcNow, null, null, null, null, null, 1m, 100m,
            Array.Empty<LeaveCashoutSettlementLineDto>());
        var posted = draft with { Status = "POSTED", PostedBy = "MOBILE_USER", PostedDate = DateTime.UtcNow };
        var service = new Mock<ILeaveService>();
        service.Setup(s => s.CreateLeaveCashoutSettlementDraftAsync(draftRequest, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        service.Setup(s => s.PostLeaveCashoutSettlementAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(posted);
        leaveServiceOverride = service.Object;

        using var created = await Send("POST", "/api/hr/leaves/balances/cashout/draft", draftRequest, adminClaim: true);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(42, (await Data(created)).GetProperty("id").GetInt64());
        using var postedResponse = await Send("POST", "/api/hr/leaves/balances/cashout/42/post", adminClaim: true);
        Assert.Equal(HttpStatusCode.OK, postedResponse.StatusCode);
        Assert.Equal("POSTED", (await Data(postedResponse)).GetProperty("status").GetString());
        service.Verify(s => s.CreateLeaveCashoutSettlementDraftAsync(draftRequest, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.PostLeaveCashoutSettlementAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveBalanceMigrationHttp_RequiresAdminAndForwardsReviewedDraftAndLifecycleIds()
    {
        var migrationDate = new DateTime(2026, 7, 1);
        var draftRequest = new CreateLeaveBalanceMigrationDto("manual-review-2026", migrationDate, 7, 1,
            new[] { new LeaveBalanceMigrationAllocationDto(501, migrationDate, new DateTime(2027, 6, 30), 12m, 2m, 1m) });
        using (var nonAdmin = await Send("POST", "/api/hr/leaves/balances/migration/draft", draftRequest, adminClaim: true))
            Assert.Equal(HttpStatusCode.Forbidden, nonAdmin.StatusCode);

        using (var setup = new ThinkOnErp.Infrastructure.Data.OracleDbContext(Options(1)))
        {
            (await setup.SysUsers.SingleAsync(user => user.Id == 12)).IsAdmin = true;
            await setup.SaveChangesAsync();
        }

        var expected = new LeaveBalanceMigrationDto(42, "manual-review-2026", migrationDate, 1, 1, 7, 1,
            "DRAFT", "MOBILE_USER", DateTime.UtcNow, null, null, null, null, Array.Empty<LeaveBalanceMigrationLineDto>());
        var service = new Mock<ILeaveService>();
        service.Setup(s => s.CreateLeaveBalanceMigrationDraftAsync(It.IsAny<CreateLeaveBalanceMigrationDto>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        service.Setup(s => s.GetLeaveBalanceMigrationAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        service.Setup(s => s.PostLeaveBalanceMigrationAsync(42, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected with { Status = "POSTED" });
        service.Setup(s => s.ReverseLeaveBalanceMigrationAsync(42, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected with { Status = "REVERSED" });
        leaveServiceOverride = service.Object;

        using var draft = await Send("POST", "/api/hr/leaves/balances/migration/draft", draftRequest, adminClaim: true);
        Assert.True(draft.StatusCode == HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        service.Verify(s => s.CreateLeaveBalanceMigrationDraftAsync(
            It.Is<CreateLeaveBalanceMigrationDto>(dto => dto.MigrationKey == "manual-review-2026" && dto.Allocations.Count == 1 &&
                dto.Allocations[0].SourceBalanceId == 501 && dto.Allocations[0].AccruedDays == 12m && dto.Allocations[0].UsedDays == 2m && dto.Allocations[0].CarriedDays == 1m),
            "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);

        using var review = await Send("GET", "/api/hr/leaves/balances/migration/42", adminClaim: true);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        using var posted = await Send("POST", "/api/hr/leaves/balances/migration/42/post", adminClaim: true);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        using var reversed = await Send("POST", "/api/hr/leaves/balances/migration/42/reverse", adminClaim: true);
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        service.Verify(s => s.GetLeaveBalanceMigrationAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.PostLeaveBalanceMigrationAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.ReverseLeaveBalanceMigrationAsync(42, "MOBILE_USER", It.IsAny<CancellationToken>()), Times.Once);
    }
}
