using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class GeofenceCheckResult
{
    public bool IsWithinRange { get; set; }
    public decimal DistanceMeters { get; set; }
    public decimal AllowedRadiusMeters { get; set; }
    public long? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = "UNKNOWN";
    public string Message { get; set; } = string.Empty;
}

public interface IGeoLocationService
{
    /// <summary>
    /// Computes the great-circle distance between two geographic coordinates in meters using the Haversine formula.
    /// </summary>
    double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Decimal overload for Haversine distance calculation.
    /// </summary>
    decimal CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2);

    /// <summary>
    /// Validates if the given coordinates are within the branch's geofence perimeter.
    /// </summary>
    GeofenceCheckResult CheckProximity(decimal punchLat, decimal punchLon, SysBranch branch, decimal? overrideAllowedRadiusMeters = null);

    /// <summary>
    /// Evaluates a list of branches and finds the nearest one to the specified coordinates.
    /// </summary>
    (SysBranch? NearestBranch, decimal DistanceMeters, bool IsWithinRange) FindNearestBranch(
        decimal punchLat,
        decimal punchLon,
        IEnumerable<SysBranch> branches);
}
