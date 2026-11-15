using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class GeoLocationService : IGeoLocationService
{
    private const double EarthRadiusMeters = 6371000.0; // Mean Earth radius in meters
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(ILogger<GeoLocationService> logger)
    {
        _logger = logger;
    }

    public double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var rLat1 = ToRadians(lat1);
        var rLat2 = ToRadians(lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        a = Math.Clamp(a, 0d, 1d);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public decimal CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var distance = CalculateDistanceMeters((double)lat1, (double)lon1, (double)lat2, (double)lon2);
        return Math.Round((decimal)distance, 2);
    }

    public GeofenceCheckResult CheckProximity(
        decimal punchLat,
        decimal punchLon,
        SysBranch branch,
        decimal? overrideAllowedRadiusMeters = null)
    {
        if (branch == null)
            throw new ArgumentNullException(nameof(branch));

        var branchName = !string.IsNullOrWhiteSpace(branch.BranchNameEn) ? branch.BranchNameEn : branch.BranchNameLocal;

        if (punchLat is < -90m or > 90m || punchLon is < -180m or > 180m)
            return new GeofenceCheckResult { IsWithinRange = false, BranchId = branch.Id, Status = "INVALID_COORDINATES", Message = "Valid GPS coordinates are required." };
        if (!branch.EnforceGeofence)
            return new GeofenceCheckResult { IsWithinRange = true, BranchId = branch.Id, BranchName = branchName,
                AllowedRadiusMeters = overrideAllowedRadiusMeters ?? branch.GeofenceRadiusMeters, Status = "GEOFENCE_DISABLED", Message = "Geofence enforcement is disabled for this branch." };
        // Missing branch coordinates are unverified, never proof of proximity.
        if (!branch.Latitude.HasValue || !branch.Longitude.HasValue)
        {
            _logger.LogWarning("Branch {BranchId} ({BranchName}) has no GPS coordinates configured.", branch.Id, branchName);
            return new GeofenceCheckResult
            {
                IsWithinRange = false,
                DistanceMeters = 0m,
                AllowedRadiusMeters = overrideAllowedRadiusMeters ?? branch.GeofenceRadiusMeters,
                BranchId = branch.Id,
                BranchName = branchName,
                Status = "NO_BRANCH_COORDINATES",
                Message = $"Branch '{branchName}' has no GPS coordinates configured. Location could not be verified; the company rejection/flag policy applies."
            };
        }

        var allowedRadius = overrideAllowedRadiusMeters ?? (branch.GeofenceRadiusMeters > 0 ? branch.GeofenceRadiusMeters : 100m);
        if (branch.Latitude is < -90m or > 90m || branch.Longitude is < -180m or > 180m || allowedRadius <= 0m)
            return new GeofenceCheckResult { IsWithinRange = false, BranchId = branch.Id, BranchName = branchName,
                Status = "INVALID_BRANCH_GEOFENCE", Message = "Branch GPS coordinates and allowed radius need configuration review." };
        var distance = CalculateDistanceMeters(punchLat, punchLon, branch.Latitude.Value, branch.Longitude.Value);
        var isWithin = distance <= allowedRadius;

        return new GeofenceCheckResult
        {
            IsWithinRange = isWithin,
            DistanceMeters = distance,
            AllowedRadiusMeters = allowedRadius,
            BranchId = branch.Id,
            BranchName = branchName,
            Status = isWithin ? "INSIDE" : "OUTSIDE_RANGE",
            Message = isWithin
                ? $"Verified inside branch '{branchName}' perimeter ({distance:F1}m <= {allowedRadius:F0}m)."
                : $"You are {distance:F1} meters away from branch '{branchName}'. Maximum allowed range is {allowedRadius:F0} meters."
        };
    }

    public (SysBranch? NearestBranch, decimal DistanceMeters, bool IsWithinRange) FindNearestBranch(
        decimal punchLat,
        decimal punchLon,
        IEnumerable<SysBranch> branches)
    {
        SysBranch? nearest = null;
        var minDistance = decimal.MaxValue;

        foreach (var branch in branches)
        {
            if (!branch.Latitude.HasValue || !branch.Longitude.HasValue)
                continue;

            var d = CalculateDistanceMeters(punchLat, punchLon, branch.Latitude.Value, branch.Longitude.Value);
            if (d < minDistance)
            {
                minDistance = d;
                nearest = branch;
            }
        }

        if (nearest == null)
            return (null, 0m, false);

        var radius = nearest.GeofenceRadiusMeters > 0 ? nearest.GeofenceRadiusMeters : 100m;
        var isInside = minDistance <= radius;

        return (nearest, minDistance, isInside);
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
