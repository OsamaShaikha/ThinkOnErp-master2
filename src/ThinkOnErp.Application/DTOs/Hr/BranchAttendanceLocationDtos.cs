namespace ThinkOnErp.Application.DTOs.Hr;
public record WriteBranchAttendanceLocationDto([property: global::System.Text.Json.Serialization.JsonRequired] decimal? Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal? Longitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal RadiusMeters,
    [property: global::System.Text.Json.Serialization.JsonRequired] bool EnforceGeofence);
public record BranchAttendanceLocationDto(long BranchId, long CompanyId, decimal? Latitude, decimal? Longitude, decimal RadiusMeters, bool EnforceGeofence,
    string Version, string CreationUser, DateTime CreationDate, string? UpdateUser, DateTime? UpdateDate);
