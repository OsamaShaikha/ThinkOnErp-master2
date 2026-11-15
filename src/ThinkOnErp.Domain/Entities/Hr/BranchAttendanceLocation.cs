using System.Globalization;
namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class BranchAttendanceLocation
{
    public long BranchId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long CompanyId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal RadiusMeters { get; set; }
    public bool EnforceGeofence { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
public sealed class BranchLocationVersionException() : InvalidOperationException("Branch attendance location changed; reload its current version.");
public static class BranchAttendanceLocationRules
{
    public static void Validate(BranchAttendanceLocation value)
    {
        if (value.Latitude is < -90m or > 90m || value.Longitude is < -180m or > 180m || value.Latitude.HasValue != value.Longitude.HasValue ||
            (value.EnforceGeofence && !value.Latitude.HasValue) || value.RadiusMeters is <= 0m or > 99999999.99m ||
            (value.Latitude.HasValue && decimal.Round(value.Latitude.Value, 7) != value.Latitude) ||
            (value.Longitude.HasValue && decimal.Round(value.Longitude.Value, 7) != value.Longitude) || decimal.Round(value.RadiusMeters, 2) != value.RadiusMeters)
        {
            throw new ArgumentException("Valid paired GPS coordinates (7 decimals) and a positive radius (2 decimals) are required; enforced geofencing needs coordinates.");
        }
    }
    public static string Version(BranchAttendanceLocation value)
    {
        static string Number(decimal? x)
        {
            return x?.ToString("G29", CultureInfo.InvariantCulture) ?? "NULL";
        }

        return LeaveMovementKey.Hash(string.Join("|", value.BranchId, Number(value.Latitude), Number(value.Longitude), Number(value.RadiusMeters),
            value.EnforceGeofence, value.CreationUser, value.CreationDate.Ticks, value.UpdateUser, value.UpdateDate?.Ticks));
    }
}
