using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;
namespace ThinkOnErp.HR.Tests;

public class MobileEvidenceAuditTests
{
    private readonly Mock<IAttendanceCorrectionRepository> raw = new();
    private readonly Mock<IEmployeeRepository> employees = new();
    private readonly Mock<IBranchRepository> legacyBranches = new(MockBehavior.Strict);
    private readonly Mock<IAttendanceBranchRepository> branches = new();
    private readonly Mock<IPolicyRepository> policies = new();
    private readonly Mock<IRawAttendanceProcessingService> processing = new();
    private readonly SysBranch branch = new() { Id = 10, CompanyId = 1, IsActive = true, EnforceGeofence = true };
    private RawAttendance? stored;
    public MobileEvidenceAuditTests()
    {
        employees.Setup(e => e.GetEmployeeByCodeAsync("E", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E", BranchId = 10 });
        branches.Setup(b => b.GetAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        raw.Setup(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long,string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawAttendance r, Func<long,string> receipt, CancellationToken ct) => { r.Id = 77; r.EventResponseJson = receipt(77); stored = r; return r; });
        raw.Setup(r => r.GetMobileEventAsync("E", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
    }
    private HrAttendanceService Service() => new(raw.Object, employees.Object, legacyBranches.Object, policies.Object,
        new GeoLocationService(NullLogger<GeoLocationService>.Instance), NullLogger<HrAttendanceService>.Instance,
        processing: processing.Object, branchAccess: branches.Object);

    [Theory]
    [InlineData("REJECT", false)]
    [InlineData("FLAG_AND_ALLOW", true)]
    public async Task MissingBranchGpsFollowsCompanyPolicyAndReplaysOriginalDecision(string action, bool accepted)
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { GeofenceViolationAction = action });
        var request = new CheckInRequestDto("E", 0m, 0m, AccuracyMeters: 12m, DeviceId: "client-device", EventId: Guid.NewGuid().ToString());
        var service = Service(); var receipt = await service.CheckInAsync(request, "USER");
        Assert.Equal(accepted, receipt.Success); Assert.Equal(accepted, receipt.EvidenceOnly); Assert.Equal(77, receipt.RawEventId);
        Assert.False(receipt.IsWithinGeofence); Assert.Equal("NO_BRANCH_COORDINATES", receipt.GeofenceStatus);
        Assert.NotNull(stored); Assert.Equal(!accepted, stored!.IsProcessed); Assert.Equal(12m, stored.AccuracyMeters);
        Assert.Equal("MOBILE_APP", stored.Source); Assert.Equal("USER", stored.CreationUser);
        Assert.Equal(accepted ? null : "REJECTED", stored.ProcessingOutcome);
        branch.EnforceGeofence = false; // A retry preserves the original rejection/flag, not the changed policy.
        Assert.Equal(receipt, await service.CheckInAsync(request, "RETRY"));
        raw.Verify(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long,string>>(), It.IsAny<CancellationToken>()), Times.Once);
        processing.Verify(p => p.ProcessAsync(77, "USER", It.IsAny<CancellationToken>()), accepted ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task DisabledCompanyGeofenceIsExplicitAndDoesNotClaimVerifiedRawGps()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });
        var receipt = await Service().CheckInAsync(new("E", 0m, 0m, EventId: Guid.NewGuid().ToString()), "USER");
        Assert.True(receipt.Success); Assert.Equal("GEOFENCE_DISABLED", stored!.GeofenceStatus); Assert.Null(stored.IsWithinGeofence);
    }

    [Theory]
    [InlineData("ONLINE")]
    [InlineData("BIOMETRIC")]
    public async Task MobileCannotForgeTrustedEventSource(string source)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(new("E", 0m, 0m, Source: source, EventId: Guid.NewGuid().ToString()), "USER"));
        Assert.Null(stored);
    }

    [Fact]
    public async Task DeniedBranchCannotReachLegacyCatalogOrCreateEvidence()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().CheckInAsync(new("E", 0m, 0m, BranchId: 99, EventId: Guid.NewGuid().ToString()), "USER"));
        Assert.Null(stored); legacyBranches.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NearbyUsesOnlyAuthorizedBranches()
    {
        branch.Latitude = 0m; branch.Longitude = 0m;
        branches.Setup(b => b.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { branch });
        var result = await Service().GetNearbyBranchesAsync(0m, 0m);
        Assert.Equal(10, Assert.Single(result).BranchId); legacyBranches.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(91, 0, 100)]
    [InlineData(0, -181, 100)]
    [InlineData(0, 0, 0)]
    public void InvalidBranchConfigurationIsUnverified(int latitude, int longitude, int radius)
    {
        branch.Latitude = latitude; branch.Longitude = longitude;
        var result = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(0, 0, branch, radius);
        Assert.False(result.IsWithinRange); Assert.Equal("INVALID_BRANCH_GEOFENCE", result.Status);
    }

    [Fact]
    public void AntipodalDistanceRemainsFinite()
    {
        var distance = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CalculateDistanceMeters(0m, 0m, 0m, 180m);
        Assert.InRange(distance, 20000000m, 20100000m);
    }

    [Theory]
    [InlineData("REJECT", false)]
    [InlineData("FLAG_AND_ALLOW", true)]
    public async Task CheckOutAsync_FollowsCompanyGeofencePolicyAndAuditEvidence(string action, bool accepted)
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { GeofenceViolationAction = action });
        var request = new CheckOutRequestDto("E", 0m, 0m, AccuracyMeters: 15m, DeviceId: "checkout-device", EventId: Guid.NewGuid().ToString());
        var service = Service();
        var receipt = await service.CheckOutAsync(request, "USER_OUT");

        Assert.Equal(accepted, receipt.Success);
        Assert.Equal(accepted, receipt.EvidenceOnly);
        Assert.Equal(77, receipt.RawEventId);
        Assert.Equal("OUT", receipt.PunchType);
        Assert.NotNull(stored);
        Assert.Equal("OUT", stored!.PunchType);
        Assert.Equal("USER_OUT", stored.CreationUser);
        Assert.Equal(15m, stored.AccuracyMeters);
        Assert.Equal("MOBILE_APP", stored.Source);
    }

    [Theory]
    [InlineData(91, 0, 10)]
    [InlineData(-91, 0, 10)]
    [InlineData(0, 181, 10)]
    [InlineData(0, -181, 10)]
    [InlineData(0, 0, -1)]
    public async Task RecordPunch_RejectsInvalidCoordinatesOrNegativeAccuracy(decimal lat, decimal lon, decimal accuracy)
    {
        var request = new CheckInRequestDto("E", lat, lon, AccuracyMeters: accuracy, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
        Assert.Null(stored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordPunch_RejectsEmptyOrWhitespaceEmployeeCode(string? empCode)
    {
        var request = new CheckInRequestDto(empCode!, 0m, 0m, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
        Assert.Null(stored);
    }

    [Fact]
    public async Task RecordPunch_ThrowsKeyNotFoundException_WhenEmployeeMissing()
    {
        employees.Setup(e => e.GetEmployeeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var request = new CheckInRequestDto("UNKNOWN", 0m, 0m, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().CheckInAsync(request, "USER"));
        Assert.Null(stored);
    }

    [Fact]
    public void CalculateDistanceMeters_ZeroDistance_WhenSameCoordinates()
    {
        var distance = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CalculateDistanceMeters(31.95m, 35.91m, 31.95m, 35.91m);
        Assert.Equal(0m, distance);
    }

    [Fact]
    public void CalculateDistanceMeters_MeridianDistance_IsAccurate()
    {
        var distance = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CalculateDistanceMeters(0m, 0m, 1m, 0m);
        Assert.InRange(distance, 110000m, 112000m);
    }

    [Fact]
    public void CheckProximity_ReturnsInside_WhenWithinConfiguredRadius()
    {
        branch.Latitude = 31.95m;
        branch.Longitude = 35.91m;
        branch.GeofenceRadiusMeters = 500m;
        var result = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(31.9501m, 35.9101m, branch, 500m);
        Assert.True(result.IsWithinRange);
        Assert.Equal("INSIDE", result.Status);
    }

    [Fact]
    public void CheckProximity_ReturnsOutside_WhenBeyondRadius()
    {
        branch.Latitude = 31.95m;
        branch.Longitude = 35.91m;
        branch.GeofenceRadiusMeters = 100m;
        // Point is ~1.5 km away
        var result = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(31.96m, 35.92m, branch, 100m);
        Assert.False(result.IsWithinRange);
        Assert.Equal("OUTSIDE_RANGE", result.Status);
    }

    [Fact]
    public void CheckProximity_ReturnsNoBranchCoordinates_WhenBranchHasNullLatitudeOrLongitude()
    {
        branch.Latitude = null;
        branch.Longitude = null;
        var result = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(31.95m, 35.91m, branch, 200m);
        Assert.False(result.IsWithinRange);
        Assert.Equal("NO_BRANCH_COORDINATES", result.Status);
    }

    [Fact]
    public async Task CheckInAsync_WhenDeviceIdSupplied_PreservesDeviceIdInRawAttendance()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckInRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString(), DeviceId: "SAMSUNG-GALAXY-S24");
        var receipt = await Service().CheckInAsync(request, "USER");

        Assert.True(receipt.Success);
        Assert.NotNull(stored);
        Assert.Equal("SAMSUNG-GALAXY-S24", stored!.DeviceId);
    }

    [Fact]
    public async Task CheckInAsync_WhenAccuracyMetersNull_DefaultsToNullWithoutThrowing()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckInRequestDto("E", 0m, 0m, AccuracyMeters: null, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckInAsync(request, "USER");

        Assert.True(receipt.Success);
        Assert.NotNull(stored);
        Assert.Null(stored!.AccuracyMeters);
    }

    [Fact]
    public async Task CheckOutAsync_WhenGeofenceDisabled_SetsStatusToGeofenceDisabledAndSucceeds()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.True(receipt.Success);
        Assert.Equal("GEOFENCE_DISABLED", receipt.GeofenceStatus);
        Assert.Null(stored!.IsWithinGeofence);
    }

    [Fact]
    public async Task CheckOutAsync_WhenBranchDenied_ThrowsKeyNotFoundException()
    {
        var request = new CheckOutRequestDto("E", 0m, 0m, BranchId: 999, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().CheckOutAsync(request, "USER"));
        Assert.Null(stored);
    }

    [Fact]
    public async Task CheckOutAsync_WhenEmployeeMissing_ThrowsKeyNotFoundException()
    {
        employees.Setup(e => e.GetEmployeeByCodeAsync("NOT_EXIST", It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var request = new CheckOutRequestDto("NOT_EXIST", 0m, 0m, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().CheckOutAsync(request, "USER"));
        Assert.Null(stored);
    }

    [Fact]
    public async Task CheckInAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), ct))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckInRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckInAsync(request, "USER", ct);

        Assert.True(receipt.Success);
        employees.Verify(e => e.GetEmployeeByCodeAsync("E", ct), Times.Once);
        raw.Verify(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), ct), Times.Once);
    }

    [Fact]
    public async Task CheckOutAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), ct))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER", ct);

        Assert.True(receipt.Success);
        employees.Verify(e => e.GetEmployeeByCodeAsync("E", ct), Times.Once);
        raw.Verify(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), ct), Times.Once);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task CheckInAsync_WhenCoordinatesOutOfBounds_ThrowsArgumentException(decimal lat, decimal lng)
    {
        var request = new CheckInRequestDto("E", lat, lng, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000)]
    public async Task CheckInAsync_WhenAccuracyInvalid_ThrowsArgumentException(decimal accuracy)
    {
        var request = new CheckInRequestDto("E", 0m, 0m, AccuracyMeters: accuracy, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckInAsync_WhenDeviceIdExceeds200Chars_ThrowsArgumentException()
    {
        var longDevice = new string('D', 201);
        var request = new CheckInRequestDto("E", 0m, 0m, DeviceId: longDevice, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckInAsync_WhenSourceExceeds100Chars_ThrowsArgumentException()
    {
        var longSource = new string('S', 101);
        var request = new CheckInRequestDto("E", 0m, 0m, Source: longSource, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckInAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckInAsync_WhenBranchInactive_RejectsPunchCleanly()
    {
        branch.IsActive = false;
        var request = new CheckInRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckInAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Equal("NO_ACTIVE_BRANCH", receipt.GeofenceStatus);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Fact]
    public async Task CheckInAsync_WhenDifferentBranchPunchNotAllowedByPolicy_RejectsPunch()
    {
        var otherBranch = new SysBranch { Id = 20, CompanyId = 1, IsActive = true, EnforceGeofence = false };
        branches.Setup(b => b.GetAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(otherBranch);
        legacyBranches.Setup(l => l.GetByIdAsync(10)).ReturnsAsync(branch);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { AllowAnyBranchPunch = false, EnforceGeofence = false, GeofenceViolationAction = "REJECT" });

        var request = new CheckInRequestDto("E", 0m, 0m, BranchId: 20, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckInAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Contains("does not allow a different branch", receipt.Message);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Fact]
    public async Task CheckInAsync_WhenGeofencePolicyActionInvalid_RejectsWithSettingsMessage()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = true, GeofenceViolationAction = "UNKNOWN_ACTION" });

        var request = new CheckInRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckInAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Contains("Geofence policy action is invalid", receipt.Message);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Fact]
    public async Task CheckOutAsync_WhenDeviceIdSupplied_PreservesDeviceIdInRawAttendance()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString(), DeviceId: "IPHONE-15-PRO");
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.True(receipt.Success);
        Assert.NotNull(stored);
        Assert.Equal("IPHONE-15-PRO", stored!.DeviceId);
    }

    [Fact]
    public async Task CheckOutAsync_WhenAccuracyMetersNull_DefaultsToNullWithoutThrowing()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var request = new CheckOutRequestDto("E", 0m, 0m, AccuracyMeters: null, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.True(receipt.Success);
        Assert.NotNull(stored);
        Assert.Null(stored!.AccuracyMeters);
    }

    [Fact]
    public async Task CheckOutAsync_WhenBranchInactive_RejectsPunchCleanly()
    {
        branch.IsActive = false;
        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Equal("NO_ACTIVE_BRANCH", receipt.GeofenceStatus);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Fact]
    public async Task CheckOutAsync_WhenDifferentBranchPunchNotAllowedByPolicy_RejectsPunch()
    {
        var otherBranch = new SysBranch { Id = 20, CompanyId = 1, IsActive = true, EnforceGeofence = false };
        branches.Setup(b => b.GetAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(otherBranch);
        legacyBranches.Setup(l => l.GetByIdAsync(10)).ReturnsAsync(branch);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { AllowAnyBranchPunch = false, EnforceGeofence = false, GeofenceViolationAction = "REJECT" });

        var request = new CheckOutRequestDto("E", 0m, 0m, BranchId: 20, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Contains("does not allow a different branch", receipt.Message);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Theory]
    [InlineData(95, 0)]
    [InlineData(-95, 0)]
    [InlineData(0, 190)]
    [InlineData(0, -190)]
    public async Task CheckOutAsync_WhenCoordinatesOutOfBounds_ThrowsArgumentException(decimal lat, decimal lng)
    {
        var request = new CheckOutRequestDto("E", lat, lng, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckOutAsync(request, "USER"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000)]
    public async Task CheckOutAsync_WhenAccuracyInvalid_ThrowsArgumentException(decimal accuracy)
    {
        var request = new CheckOutRequestDto("E", 0m, 0m, AccuracyMeters: accuracy, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckOutAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckOutAsync_WhenDeviceIdExceeds200Chars_ThrowsArgumentException()
    {
        var longDevice = new string('D', 201);
        var request = new CheckOutRequestDto("E", 0m, 0m, DeviceId: longDevice, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckOutAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckOutAsync_WhenSourceExceeds100Chars_ThrowsArgumentException()
    {
        var longSource = new string('S', 101);
        var request = new CheckOutRequestDto("E", 0m, 0m, Source: longSource, EventId: Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CheckOutAsync(request, "USER"));
    }

    [Fact]
    public async Task CheckOutAsync_WhenGeofencePolicyActionInvalid_RejectsWithSettingsMessage()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = true, GeofenceViolationAction = "INVALID_CHOICE" });

        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        var receipt = await Service().CheckOutAsync(request, "USER");

        Assert.False(receipt.Success);
        Assert.Contains("Geofence policy action is invalid", receipt.Message);
        Assert.NotNull(stored);
        Assert.Equal("REJECTED", stored!.ProcessingOutcome);
    }

    [Fact]
    public void GeoLocationService_CheckProximity_NegativeRadius_ReturnsInvalidBranchGeofence()
    {
        branch.Latitude = 31.95m;
        branch.Longitude = 35.91m;
        var result = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(31.95m, 35.91m, branch, -10m);
        Assert.False(result.IsWithinRange);
        Assert.Equal("INVALID_BRANCH_GEOFENCE", result.Status);
    }

    [Fact]
    public void GeoLocationService_CalculateDistanceMeters_NegativeCoordinates_ReturnsAccurateDistance()
    {
        // 1 degree along meridian in southern hemisphere
        var distance = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CalculateDistanceMeters(-33.86m, 151.2m, -33.87m, 151.2m);
        Assert.InRange(distance, 1100m, 1120m);
    }

    [Fact]
    public async Task CheckInAsync_WhenOfflinePunchReplayWithIdenticalEvent_ReturnsCachedReceiptIdempotently()
    {
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendancePolicy { EnforceGeofence = false });

        var eventGuid = Guid.NewGuid().ToString();
        var request = new CheckInRequestDto("E", 0m, 0m, EventId: eventGuid);
        var service = Service();

        var firstReceipt = await service.CheckInAsync(request, "USER");
        Assert.True(firstReceipt.Success);
        Assert.NotNull(stored);

        // Second call with same event Guid should hit GetMobileEventAsync and return cached receipt
        var secondReceipt = await service.CheckInAsync(request, "RETRY_ACTOR");
        Assert.Equal(firstReceipt.Success, secondReceipt.Success);
        Assert.Equal(firstReceipt.RawEventId, secondReceipt.RawEventId);
        raw.Verify(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
