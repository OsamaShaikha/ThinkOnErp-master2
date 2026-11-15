using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeaveAccrualBackgroundServiceTests
{
    private readonly Mock<ILeaveRepository> _leaveRepository = new();
    private readonly Mock<ILeaveService> _leaveService = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    private readonly LeaveAccrualBackgroundService _service;

    public LeaveAccrualBackgroundServiceTests()
    {
        _service = new LeaveAccrualBackgroundService(
            _scopeFactory.Object,
            _configuration,
            NullLogger<LeaveAccrualBackgroundService>.Instance);
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenDisabled_SkipsAccrual()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = false,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TEST_SCHEMA",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(_service._completedLocalDates.ContainsKey("TEST_SCHEMA"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenTimeZoneNullOrWhitespace_LogsWarningAndSkips(string? timeZoneId)
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = timeZoneId
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TEST_SCHEMA",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(_service._completedLocalDates.ContainsKey("TEST_SCHEMA"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenTimeZoneInvalid_LogsWarningAndSkipsWithoutThrowing()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "Invalid/Unknown_TimeZone_XYZ"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TEST_SCHEMA",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(_service._completedLocalDates.ContainsKey("TEST_SCHEMA"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenAlreadyCompletedForLocalDate_SkipsAccrualDueToDailyIdempotency()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // November 8, 2026 12:00 UTC -> last completed local day is November 7, 2026
        var completedLocalDate = new DateTime(2026, 11, 7);
        _service._completedLocalDates["TEST_SCHEMA"] = completedLocalDate;

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TEST_SCHEMA",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenEnabledAndValid_RunsDailyMonthlyYearlyForActivePoliciesOnly()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual Policy", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 5, 21m, 30m, true),
            new(2, "Sick Policy", "SICK", "NONE", 0m, "ALL", 0, 0, 14m, 14m, false)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var successResult = new LeaveAccrualResultDto(10, 5, 5.5m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        var testUtc = new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc);
        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_ALPHA",
            _leaveRepository.Object,
            _leaveService.Object,
            testUtc,
            CancellationToken.None);

        // Verify ANNUAL was run for DAILY, MONTHLY, and YEARLY
        _leaveService.Verify(s => s.RunDailyAccrualAsync(
            It.Is<DailyLeaveAccrualDto>(d => d.LeaveTypeCode == "ANNUAL" && d.AsOfDate == new DateTime(2026, 11, 7)),
            "SYSTEM:HR_LEAVE_ACCRUAL",
            It.IsAny<CancellationToken>()), Times.Once);

        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(
            It.Is<MonthlyLeaveAccrualDto>(d => d.LeaveTypeCode == "ANNUAL" && d.YearNo == 2026 && d.MonthNo == 10),
            "SYSTEM:HR_LEAVE_ACCRUAL",
            It.IsAny<CancellationToken>()), Times.Once);

        _leaveService.Verify(s => s.RunYearlyAllocationAsync(
            It.Is<YearlyLeaveAllocationDto>(d => d.LeaveTypeCode == "ANNUAL" && d.YearNo == 2026),
            "SYSTEM:HR_LEAVE_ACCRUAL",
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify SICK (inactive) was never run
        _leaveService.Verify(s => s.RunDailyAccrualAsync(
            It.Is<DailyLeaveAccrualDto>(d => d.LeaveTypeCode == "SICK"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify completed local date was recorded
        Assert.True(_service._completedLocalDates.TryGetValue("TENANT_ALPHA", out var completedDate));
        Assert.Equal(new DateTime(2026, 11, 7), completedDate);
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenAccrualFails_CatchesExceptionAndDoesNotMarkCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual Policy", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 5, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient database deadlock"));

        var testUtc = new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc);
        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_BETA",
            _leaveRepository.Object,
            _leaveService.Object,
            testUtc,
            CancellationToken.None);

        // Verify that failure did NOT mark the date as completed, so the cycle can retry later
        Assert.False(_service._completedLocalDates.ContainsKey("TENANT_BETA"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenMultipleActivePolicies_DeduplicatesCodesAndProcessesAll()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 5, 21m, 30m, true),
            new(2, "Emergency", "EMERGENCY", "YEARLY", 5m, "ALL", 0, 0, 5m, 5m, true),
            new(3, "Annual duplicate version", "annual", "MONTHLY", 2.5m, "ALL", 0, 5, 30m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var successResult = new LeaveAccrualResultDto(5, 5, 2.5m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_GAMMA",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Distinct codes: ANNUAL and EMERGENCY (case-insensitive deduplication)
        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.Is<DailyLeaveAccrualDto>(d => d.LeaveTypeCode == "ANNUAL"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.Is<DailyLeaveAccrualDto>(d => d.LeaveTypeCode == "EMERGENCY"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_service._completedLocalDates.ContainsKey("TENANT_GAMMA"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenOneAccrualKindFails_ContinuesOthersAndDoesNotMarkCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 5, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var successResult = new LeaveAccrualResultDto(1, 1, 1m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Monthly calculation error"));
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_PARTIAL",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Daily and Yearly were still called despite Monthly failure
        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _leaveService.Verify(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Crucial: because one kind failed, date must NOT be marked completed, allowing retry later
        Assert.False(_service._completedLocalDates.ContainsKey("TENANT_PARTIAL"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenNoActivePolicies_DoesNotCallAccrualMethodsAndMarksCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Inactive 1", "TYPE1", "MONTHLY", 1m, "ALL", 0, 0, 10m, 10m, false),
            new(2, "Inactive 2", "TYPE2", "YEARLY", 2m, "ALL", 0, 0, 20m, 20m, false)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_INACTIVE",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Date should be marked completed because there was no failure
        Assert.True(_service._completedLocalDates.ContainsKey("TENANT_INACTIVE"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenZeroBalancesUpdated_MarksCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "DAILY", 0.05m, "ALL", 0, 0, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var zeroResult = new LeaveAccrualResultDto(0, 0, 0m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zeroResult);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zeroResult);
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zeroResult);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "TENANT_ZERO",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.True(_service._completedLocalDates.ContainsKey("TENANT_ZERO"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "DAILY", 0.05m, "ALL", 0, 0, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessTenantSettingsAndAccrualsAsync(
                "TENANT_CANCEL",
                _leaveRepository.Object,
                _leaveService.Object,
                new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
                cts.Token));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenDifferentSchemasProcessed_TracksCompletedDatesPerSchema()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 0, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var successResult = new LeaveAccrualResultDto(1, 1, 1m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Process Schema 1
        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_ONE",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Process Schema 2
        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_TWO",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.True(_service._completedLocalDates.ContainsKey("SCHEMA_ONE"));
        Assert.True(_service._completedLocalDates.ContainsKey("SCHEMA_TWO"));
        Assert.Equal(2, _service._completedLocalDates.Count);
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenPoliciesEmpty_DoesNotRunAnyAccrual_AndMarksDayCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicyDetailsDto>());

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_EMPTY_POLICIES",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(_service._completedLocalDates.ContainsKey("SCHEMA_EMPTY_POLICIES"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenPoliciesHaveOnlyInactiveTypes_SkipsAccrual_AndMarksDayCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "MONTHLY", 1.75m, "ALL", 0, 0, 21m, 30m, false), // IsActive = false
            new(2, "Sick", "SICK", "YEARLY", 14m, "ALL", 0, 0, 14m, 14m, false)         // IsActive = false
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_INACTIVE_POLICIES",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveService.Verify(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(_service._completedLocalDates.ContainsKey("SCHEMA_INACTIVE_POLICIES"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenMultipleActivePoliciesShareSameCode_RunsAccrualOncePerCode()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual 1", "ANNUAL", "DAILY", 0.05m, "ALL", 0, 0, 21m, 30m, true),
            new(2, "Annual 2", "annual", "DAILY", 0.08m, "ALL", 0, 0, 30m, 30m, true) // Same code in lowercase
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var result = new LeaveAccrualResultDto(1, 1, 1m, new List<string>());
        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _leaveService.Setup(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _leaveService.Setup(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_DUPLICATE_CODES",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        _leaveService.Verify(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), "SYSTEM:HR_LEAVE_ACCRUAL", It.IsAny<CancellationToken>()), Times.Once);
        _leaveService.Verify(s => s.RunMonthlyAccrualAsync(It.IsAny<MonthlyLeaveAccrualDto>(), "SYSTEM:HR_LEAVE_ACCRUAL", It.IsAny<CancellationToken>()), Times.Once);
        _leaveService.Verify(s => s.RunYearlyAllocationAsync(It.IsAny<YearlyLeaveAllocationDto>(), "SYSTEM:HR_LEAVE_ACCRUAL", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_service._completedLocalDates.ContainsKey("SCHEMA_DUPLICATE_CODES"));
    }

    [Fact]
    public async Task ProcessTenantSettingsAndAccrualsAsync_WhenAccrualFails_DoesNotMarkDayAsCompleted()
    {
        var settings = new HrLeaveSettings
        {
            AutomaticAccrualEnabled = true,
            AccrualTimeZoneId = "UTC"
        };
        _leaveRepository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var policies = new List<LeavePolicyDetailsDto>
        {
            new(1, "Annual", "ANNUAL", "DAILY", 0.05m, "ALL", 0, 0, 21m, 30m, true)
        };
        _leaveService.Setup(s => s.GetLeavePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        _leaveService.Setup(s => s.RunDailyAccrualAsync(It.IsAny<DailyLeaveAccrualDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database failure during daily accrual"));

        await _service.ProcessTenantSettingsAndAccrualsAsync(
            "SCHEMA_FAIL",
            _leaveRepository.Object,
            _leaveService.Object,
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.False(_service._completedLocalDates.ContainsKey("SCHEMA_FAIL"));
    }
}
