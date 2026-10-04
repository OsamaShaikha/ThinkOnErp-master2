using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

public class AttendanceGeofencingTests
{
    private readonly GeoLocationService _geoService;

    // Riyadh Headquarters sample coordinates: Lat: 24.7136000, Lon: 46.6753000
    private const decimal BranchLat = 24.7136000m;
    private const decimal BranchLon = 46.6753000m;

    public AttendanceGeofencingTests()
    {
        _geoService = new GeoLocationService(NullLogger<GeoLocationService>.Instance);
    }

    [Fact]
    public void CalculateDistance_SamePoint_ReturnsZero()
    {
        var distance = _geoService.CalculateDistanceMeters(BranchLat, BranchLon, BranchLat, BranchLon);
        Assert.Equal(0.0m, distance);
    }

    [Fact]
    public void CalculateDistance_NearbyPoint_ReturnsAccurateMeters()
    {
        // Approximately 55-60 meters away (0.0005 deg shift in latitude ~ 55.6m)
        decimal nearbyLat = BranchLat + 0.0005m;
        decimal nearbyLon = BranchLon;

        var distance = _geoService.CalculateDistanceMeters(BranchLat, BranchLon, nearbyLat, nearbyLon);

        Assert.InRange(distance, 50m, 60m);
    }

    [Fact]
    public void CheckProximity_WithinAllowedRadius_ReturnsInside()
    {
        var branch = new SysBranch
        {
            Id = 1,
            BranchNameEn = "Riyadh HQ",
            BranchNameLocal = "الفرع الرئيسي بالرياض",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        // Punch location 55m away
        decimal punchLat = BranchLat + 0.0005m;
        decimal punchLon = BranchLon;

        var result = _geoService.CheckProximity(punchLat, punchLon, branch);

        Assert.True(result.IsWithinRange);
        Assert.Equal("INSIDE", result.Status);
        Assert.InRange(result.DistanceMeters, 50m, 60m);
        Assert.Equal(100m, result.AllowedRadiusMeters);
    }

    [Fact]
    public void CheckProximity_OutsideAllowedRadius_ReturnsOutsideRange()
    {
        var branch = new SysBranch
        {
            Id = 1,
            BranchNameEn = "Riyadh HQ",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        // Punch location ~550m away (0.005 deg latitude)
        decimal punchLat = BranchLat + 0.005m;
        decimal punchLon = BranchLon;

        var result = _geoService.CheckProximity(punchLat, punchLon, branch);

        Assert.False(result.IsWithinRange);
        Assert.Equal("OUTSIDE_RANGE", result.Status);
        Assert.True(result.DistanceMeters > 500m);
        Assert.Contains("Maximum allowed range is 100", result.Message);
    }

    [Fact]
    public void CheckProximity_BranchHasNoCoordinates_ReturnsPermissive()
    {
        var branch = new SysBranch
        {
            Id = 2,
            BranchNameEn = "Virtual Branch",
            Latitude = null,
            Longitude = null,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        var result = _geoService.CheckProximity(BranchLat, BranchLon, branch);

        Assert.True(result.IsWithinRange);
        Assert.Equal("NO_BRANCH_COORDINATES", result.Status);
    }

    [Fact]
    public void CheckProximity_GeofenceDisabled_ReturnsPermissive()
    {
        var branch = new SysBranch
        {
            Id = 3,
            BranchNameEn = "Remote Branch",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 50m,
            EnforceGeofence = false
        };

        // 10km away
        decimal punchLat = BranchLat + 0.1m;
        decimal punchLon = BranchLon;

        var result = _geoService.CheckProximity(punchLat, punchLon, branch);

        Assert.True(result.IsWithinRange);
        Assert.Equal("GEOFENCE_DISABLED", result.Status);
    }

    [Fact]
    public void FindNearestBranch_SelectsClosestConfiguredBranch()
    {
        var branchHq = new SysBranch
        {
            Id = 1,
            BranchNameEn = "HQ",
            Latitude = 24.7136m,
            Longitude = 46.6753m,
            GeofenceRadiusMeters = 100m
        };

        var branchJeddah = new SysBranch
        {
            Id = 2,
            BranchNameEn = "Jeddah",
            Latitude = 21.5433m,
            Longitude = 39.1728m,
            GeofenceRadiusMeters = 100m
        };

        var branches = new List<SysBranch> { branchJeddah, branchHq };

        // Point near HQ in Riyadh
        var (nearest, distance, isInside) = _geoService.FindNearestBranch(24.7137m, 46.6754m, branches);

        Assert.NotNull(nearest);
        Assert.Equal(1, nearest!.Id);
        Assert.True(isInside);
        Assert.True(distance < 50m);
    }

    [Fact]
    public async Task HrAttendanceService_CheckInWithinRange_SucceedsAndRecordsLocation()
    {
        var mockAttendanceRepo = new Mock<IAttendanceCorrectionRepository>();
        var mockEmployeeRepo = new Mock<IEmployeeRepository>();
        var mockBranchRepo = new Mock<IBranchRepository>();
        var mockPolicyRepo = new Mock<IPolicyRepository>();

        var employee = new Employee
        {
            EmployeeCode = "EMP001",
            NameEn = "John Doe",
            BranchId = 10
        };

        var branch = new SysBranch
        {
            Id = 10,
            CompanyId = 1,
            BranchNameEn = "Main Branch",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        var policy = new AttendancePolicy
        {
            CompanyId = 1,
            EnforceGeofence = true,
            DefaultAllowedRadiusMeters = 100,
            GeofenceViolationAction = "REJECT"
        };

        mockEmployeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        mockBranchRepo.Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(branch);

        mockPolicyRepo.Setup(r => r.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        mockAttendanceRepo.Setup(r => r.GetAttendanceDayAsync("EMP001", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay?)null);

        RawAttendance? savedRaw = null;
        mockAttendanceRepo.Setup(r => r.AddRawAttendanceAsync(It.IsAny<RawAttendance>(), It.IsAny<CancellationToken>()))
            .Callback<RawAttendance, CancellationToken>((r, _) => savedRaw = r)
            .Returns(Task.CompletedTask);

        AttendanceDay? savedDay = null;
        mockAttendanceRepo.Setup(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()))
            .Callback<AttendanceDay, CancellationToken>((d, _) => savedDay = d)
            .Returns(Task.CompletedTask);

        var service = new HrAttendanceService(
            mockAttendanceRepo.Object,
            mockEmployeeRepo.Object,
            mockBranchRepo.Object,
            mockPolicyRepo.Object,
            _geoService,
            NullLogger<HrAttendanceService>.Instance);

        var checkInDto = new CheckInRequestDto(
            EmployeeCode: "EMP001",
            Latitude: BranchLat + 0.0003m, // ~33m away
            Longitude: BranchLon,
            BranchId: 10
        );

        var result = await service.CheckInAsync(checkInDto, "testuser");

        Assert.True(result.Success);
        Assert.True(result.IsWithinGeofence);
        Assert.Equal("INSIDE", result.GeofenceStatus);
        Assert.Equal("IN", result.PunchType);

        Assert.NotNull(savedRaw);
        Assert.Equal(10, savedRaw!.BranchId);
        Assert.True(savedRaw.IsWithinGeofence);
        Assert.Equal(BranchLat + 0.0003m, savedRaw.Latitude);

        Assert.NotNull(savedDay);
        Assert.Equal("EMP001", savedDay!.EmployeeCode);
        Assert.True(savedDay.IsCheckInWithinGeofence);
        Assert.False(savedDay.HasGeofenceViolation);
        Assert.Equal(10, savedDay.CheckInBranchId);
    }

    [Fact]
    public async Task HrAttendanceService_CheckInOutOfRange_WithRejectPolicy_BlocksPunch()
    {
        var mockAttendanceRepo = new Mock<IAttendanceCorrectionRepository>();
        var mockEmployeeRepo = new Mock<IEmployeeRepository>();
        var mockBranchRepo = new Mock<IBranchRepository>();
        var mockPolicyRepo = new Mock<IPolicyRepository>();

        var employee = new Employee { EmployeeCode = "EMP002", BranchId = 10 };
        var branch = new SysBranch
        {
            Id = 10,
            CompanyId = 1,
            BranchNameEn = "Main Branch",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        var policy = new AttendancePolicy
        {
            CompanyId = 1,
            EnforceGeofence = true,
            DefaultAllowedRadiusMeters = 100,
            GeofenceViolationAction = "REJECT"
        };

        mockEmployeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        mockBranchRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(branch);
        mockPolicyRepo.Setup(r => r.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var service = new HrAttendanceService(
            mockAttendanceRepo.Object,
            mockEmployeeRepo.Object,
            mockBranchRepo.Object,
            mockPolicyRepo.Object,
            _geoService,
            NullLogger<HrAttendanceService>.Instance);

        var checkInDto = new CheckInRequestDto(
            EmployeeCode: "EMP002",
            Latitude: BranchLat + 0.01m, // ~1.1 km away
            Longitude: BranchLon,
            BranchId: 10
        );

        var result = await service.CheckInAsync(checkInDto, "testuser");

        Assert.False(result.Success);
        Assert.False(result.IsWithinGeofence);
        Assert.Equal("OUTSIDE_RANGE", result.GeofenceStatus);
        Assert.Contains("away from branch", result.Message);

        // Verify no raw punch or attendance day was saved
        mockAttendanceRepo.Verify(r => r.AddRawAttendanceAsync(It.IsAny<RawAttendance>(), It.IsAny<CancellationToken>()), Times.Never);
        mockAttendanceRepo.Verify(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HrAttendanceService_CheckInOutOfRange_WithFlagPolicy_AllowsPunchWithViolationFlag()
    {
        var mockAttendanceRepo = new Mock<IAttendanceCorrectionRepository>();
        var mockEmployeeRepo = new Mock<IEmployeeRepository>();
        var mockBranchRepo = new Mock<IBranchRepository>();
        var mockPolicyRepo = new Mock<IPolicyRepository>();

        var employee = new Employee { EmployeeCode = "EMP003", BranchId = 10 };
        var branch = new SysBranch
        {
            Id = 10,
            CompanyId = 1,
            BranchNameEn = "Flexible Branch",
            Latitude = BranchLat,
            Longitude = BranchLon,
            GeofenceRadiusMeters = 100m,
            EnforceGeofence = true
        };

        var policy = new AttendancePolicy
        {
            CompanyId = 1,
            EnforceGeofence = true,
            DefaultAllowedRadiusMeters = 100,
            GeofenceViolationAction = "FLAG_AND_ALLOW"
        };

        mockEmployeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        mockBranchRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(branch);
        mockPolicyRepo.Setup(r => r.GetActiveAttendancePolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        AttendanceDay? savedDay = null;
        mockAttendanceRepo.Setup(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()))
            .Callback<AttendanceDay, CancellationToken>((d, _) => savedDay = d)
            .Returns(Task.CompletedTask);

        var service = new HrAttendanceService(
            mockAttendanceRepo.Object,
            mockEmployeeRepo.Object,
            mockBranchRepo.Object,
            mockPolicyRepo.Object,
            _geoService,
            NullLogger<HrAttendanceService>.Instance);

        var checkInDto = new CheckInRequestDto(
            EmployeeCode: "EMP003",
            Latitude: BranchLat + 0.01m, // ~1.1 km away
            Longitude: BranchLon,
            BranchId: 10
        );

        var result = await service.CheckInAsync(checkInDto, "testuser");

        Assert.True(result.Success);
        Assert.False(result.IsWithinGeofence);
        Assert.NotNull(savedDay);
        Assert.True(savedDay!.HasGeofenceViolation);
        Assert.False(savedDay.IsCheckInWithinGeofence);
    }
}
