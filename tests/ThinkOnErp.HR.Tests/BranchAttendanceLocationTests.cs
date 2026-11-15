using ThinkOnErp.Domain.Entities.Hr;
using Xunit;
namespace ThinkOnErp.HR.Tests;
public class BranchAttendanceLocationTests
{
    [Theory]
    [InlineData(91, 0, 100, true)]
    [InlineData(0, 181, 100, true)]
    [InlineData(0, 0, 0, true)]
    [InlineData(0, 0, -1, false)]
    public void RejectInvalidCoordinatesOrRadius(int latitude, int longitude, int radius, bool enforce)
        => Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(new() { Latitude = latitude, Longitude = longitude, RadiusMeters = radius, EnforceGeofence = enforce }));
    [Fact] public void EnforcementRequiresPairedCoordinates()
    {
        Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(new() { RadiusMeters = 100m, EnforceGeofence = true }));
        Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(new() { Latitude = 0m, RadiusMeters = 100m }));
        BranchAttendanceLocationRules.Validate(new() { RadiusMeters = 100m, EnforceGeofence = false });
        BranchAttendanceLocationRules.Validate(new() { Latitude = 0m, Longitude = 0m, RadiusMeters = 100m, EnforceGeofence = true });
    }
    [Fact] public void RejectDatabaseRoundingThatCouldMoveBoundary()
    {
        Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(new() { Latitude = .12345678m, Longitude = 0m, RadiusMeters = 100m }));
        Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(new() { RadiusMeters = 100.001m }));
    }
    [Fact] public void VersionSurvivesOracleScaleAndKindButChangesWithConfiguration()
    {
        var x = new BranchAttendanceLocation { BranchId = 1, CompanyId = 1, Latitude = 0.0m, Longitude = 0m, RadiusMeters = 100m, CreationUser = "HR", CreationDate = DateTime.UtcNow };
        var version = BranchAttendanceLocationRules.Version(x); x.CreationDate = DateTime.SpecifyKind(x.CreationDate, DateTimeKind.Unspecified); x.RadiusMeters = 100.00m;
        Assert.Equal(version, BranchAttendanceLocationRules.Version(x)); x.EnforceGeofence = true; Assert.NotEqual(version, BranchAttendanceLocationRules.Version(x));
    }

    [Fact]
    public void Validate_WhenValidCoordinatesAndRadius_PassesValidation()
    {
        var validLocation = new BranchAttendanceLocation
        {
            BranchId = 10,
            CompanyId = 1,
            Latitude = 31.9539494m,
            Longitude = 35.9106351m,
            RadiusMeters = 250.75m,
            EnforceGeofence = true
        };

        // Should not throw
        BranchAttendanceLocationRules.Validate(validLocation);
    }

    [Fact]
    public void Validate_WhenRadiusExceedsMaxLimit_ThrowsArgumentException()
    {
        var invalid = new BranchAttendanceLocation
        {
            Latitude = 31.95m,
            Longitude = 35.91m,
            RadiusMeters = 100000000.00m, // > 99,999,999.99m
            EnforceGeofence = true
        };

        var ex = Assert.Throws<System.ArgumentException>(() => BranchAttendanceLocationRules.Validate(invalid));
        Assert.Contains("positive radius", ex.Message);
    }

    [Theory]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    [InlineData(0.0, 0.0)]
    public void Validate_BoundaryLatitudeAndLongitude_PassesExactlyAtLimits(double lat, double lon)
    {
        var location = new BranchAttendanceLocation
        {
            Latitude = (decimal)lat,
            Longitude = (decimal)lon,
            RadiusMeters = 100m,
            EnforceGeofence = true
        };

        BranchAttendanceLocationRules.Validate(location);
    }

    [Theory]
    [InlineData(-90.0000001, 35.0)]
    [InlineData(90.0000001, 35.0)]
    [InlineData(31.0, -180.0000001)]
    [InlineData(31.0, 180.0000001)]
    public void Validate_WhenLatitudeOrLongitudeExceedsRange_ThrowsArgumentException(double lat, double lon)
    {
        var location = new BranchAttendanceLocation
        {
            Latitude = (decimal)lat,
            Longitude = (decimal)lon,
            RadiusMeters = 100m,
            EnforceGeofence = true
        };

        Assert.Throws<System.ArgumentException>(() => BranchAttendanceLocationRules.Validate(location));
    }

    [Fact]
    public void Version_ProducesDeterministicHash_ForSameProperties()
    {
        var fixedDate = new System.DateTime(2026, 11, 8, 12, 0, 0, System.DateTimeKind.Utc);
        var loc1 = new BranchAttendanceLocation
        {
            CompanyId = 5,
            BranchId = 12,
            Latitude = 32.1234567m,
            Longitude = 36.7654321m,
            RadiusMeters = 500m,
            EnforceGeofence = true,
            CreationUser = "ADMIN",
            CreationDate = fixedDate
        };

        var loc2 = new BranchAttendanceLocation
        {
            CompanyId = 5,
            BranchId = 12,
            Latitude = 32.1234567m,
            Longitude = 36.7654321m,
            RadiusMeters = 500m,
            EnforceGeofence = true,
            CreationUser = "ADMIN",
            CreationDate = fixedDate
        };

        var v1 = BranchAttendanceLocationRules.Version(loc1);
        var v2 = BranchAttendanceLocationRules.Version(loc2);

        Assert.Equal(v1, v2);
        Assert.False(string.IsNullOrWhiteSpace(v1));
    }

    [Fact]
    public void Version_ChangesWhenCoordinatesOrRadiusChange()
    {
        var loc = new BranchAttendanceLocation
        {
            CompanyId = 1,
            BranchId = 2,
            Latitude = 31.0m,
            Longitude = 35.0m,
            RadiusMeters = 100m,
            CreationUser = "HR"
        };

        var originalVersion = BranchAttendanceLocationRules.Version(loc);

        // Change latitude
        loc.Latitude = 31.0000001m;
        var vLatitudeChanged = BranchAttendanceLocationRules.Version(loc);
        Assert.NotEqual(originalVersion, vLatitudeChanged);

        // Change radius
        loc.RadiusMeters = 200m;
        var vRadiusChanged = BranchAttendanceLocationRules.Version(loc);
        Assert.NotEqual(vLatitudeChanged, vRadiusChanged);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(31.95, null)]
    [InlineData(null, 35.91)]
    public void Validate_WhenEnforceGeofenceTrue_AndAnyCoordinateNull_ThrowsArgumentException(double? lat, double? lon)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = (decimal?)lat,
            Longitude = (decimal?)lon,
            RadiusMeters = 100m,
            EnforceGeofence = true
        };

        var ex = Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(loc));
        Assert.Contains("enforced geofencing needs coordinates", ex.Message);
    }

    [Fact]
    public void Validate_WhenEnforceGeofenceFalse_AndCoordinatesNull_PassesValidation()
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = null,
            Longitude = null,
            RadiusMeters = 150m,
            EnforceGeofence = false
        };

        // Should not throw
        BranchAttendanceLocationRules.Validate(loc);
    }

    [Theory]
    [InlineData(100.001)]
    [InlineData(50.1234)]
    [InlineData(0.005)]
    public void Validate_Theory_InvalidRadiusDecimals_ThrowsArgumentException(double radius)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = 31.95m,
            Longitude = 35.91m,
            RadiusMeters = (decimal)radius,
            EnforceGeofence = true
        };

        var ex = Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(loc));
        Assert.Contains("positive radius (2 decimals)", ex.Message);
    }

    [Theory]
    [InlineData(31.12345678, 35.0)]
    [InlineData(0.00000001, 35.0)]
    [InlineData(-15.123456789, 35.0)]
    public void Validate_Theory_InvalidLatitudeDecimals_ThrowsArgumentException(double lat, double lon)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = (decimal)lat,
            Longitude = (decimal)lon,
            RadiusMeters = 100m,
            EnforceGeofence = true
        };

        var ex = Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(loc));
        Assert.Contains("GPS coordinates (7 decimals)", ex.Message);
    }

    [Theory]
    [InlineData(31.0, 35.12345678)]
    [InlineData(31.0, 0.00000001)]
    [InlineData(31.0, -179.12345678)]
    public void Validate_Theory_InvalidLongitudeDecimals_ThrowsArgumentException(double lat, double lon)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = (decimal)lat,
            Longitude = (decimal)lon,
            RadiusMeters = 100m,
            EnforceGeofence = true
        };

        var ex = Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(loc));
        Assert.Contains("GPS coordinates (7 decimals)", ex.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(-100.0)]
    public void Validate_Theory_ZeroAndNegativeRadius_ThrowsArgumentException(double radius)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = 31.0m,
            Longitude = 35.0m,
            RadiusMeters = (decimal)radius,
            EnforceGeofence = false
        };

        var ex = Assert.Throws<ArgumentException>(() => BranchAttendanceLocationRules.Validate(loc));
        Assert.Contains("positive radius", ex.Message);
    }

    [Theory]
    [InlineData(90.0000000, 180.0000000)]
    [InlineData(-90.0000000, -180.0000000)]
    [InlineData(0.1234567, 0.7654321)]
    public void Validate_Theory_ExactSevenDecimals_PassesValidation(double lat, double lon)
    {
        var loc = new BranchAttendanceLocation
        {
            Latitude = (decimal)lat,
            Longitude = (decimal)lon,
            RadiusMeters = 250.50m,
            EnforceGeofence = true
        };

        BranchAttendanceLocationRules.Validate(loc);
    }

    [Fact]
    public void Version_ChangesWhenAnyPropertyChanges()
    {
        var baseDate = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var loc1 = new BranchAttendanceLocation
        {
            CompanyId = 1,
            BranchId = 10,
            Latitude = 31.95m,
            Longitude = 35.91m,
            RadiusMeters = 100m,
            EnforceGeofence = true,
            CreationUser = "ADMIN",
            CreationDate = baseDate,
            UpdateUser = null,
            UpdateDate = null
        };

        var loc2 = new BranchAttendanceLocation
        {
            CompanyId = 1,
            BranchId = 10,
            Latitude = 31.95m,
            Longitude = 35.91m,
            RadiusMeters = 100m,
            EnforceGeofence = true,
            CreationUser = "ADMIN",
            CreationDate = baseDate,
            UpdateUser = null,
            UpdateDate = null
        };

        var v1 = BranchAttendanceLocationRules.Version(loc1);
        var v2 = BranchAttendanceLocationRules.Version(loc2);
        Assert.Equal(v1, v2);

        // CompanyId
        loc2.CompanyId = 2;
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.CompanyId = loc1.CompanyId;

        // BranchId
        loc2.BranchId = 20;
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.BranchId = loc1.BranchId;

        // EnforceGeofence
        loc2.EnforceGeofence = false;
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.EnforceGeofence = loc1.EnforceGeofence;

        // CreationUser
        loc2.CreationUser = "NEW_USER";
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.CreationUser = loc1.CreationUser;

        // CreationDate
        loc2.CreationDate = baseDate.AddDays(1);
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.CreationDate = loc1.CreationDate;

        // UpdateUser
        loc2.UpdateUser = "UPDATER";
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
        loc2.UpdateUser = null;

        // UpdateDate
        loc2.UpdateDate = baseDate.AddHours(2);
        Assert.NotEqual(v1, BranchAttendanceLocationRules.Version(loc2));
    }

    [Fact]
    public void Version_WhenCoordinatesNull_ProducesDeterministicHash()
    {
        var loc1 = new BranchAttendanceLocation
        {
            CompanyId = 1,
            BranchId = 10,
            Latitude = null,
            Longitude = null,
            RadiusMeters = 100m,
            EnforceGeofence = false,
            CreationUser = "ADMIN",
            CreationDate = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc)
        };

        var loc2 = new BranchAttendanceLocation
        {
            CompanyId = 1,
            BranchId = 10,
            Latitude = null,
            Longitude = null,
            RadiusMeters = 100m,
            EnforceGeofence = false,
            CreationUser = "ADMIN",
            CreationDate = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc)
        };

        var v1 = BranchAttendanceLocationRules.Version(loc1);
        var v2 = BranchAttendanceLocationRules.Version(loc2);

        Assert.Equal(v1, v2);
        Assert.False(string.IsNullOrWhiteSpace(v1));
    }

    [Fact]
    public void BranchLocationVersionException_InstantiatesWithExpectedMessage()
    {
        var ex = new BranchLocationVersionException();
        Assert.Contains("Branch attendance location changed", ex.Message);
    }
}
