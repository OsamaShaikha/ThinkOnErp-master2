using System.Text.Json;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class MobilePunchEventConflictException() : AttendanceEventConflictException();

public record MobilePunchEventKey(string Id, string PayloadHash)
{
    public static MobilePunchEventKey Create(string? eventId, string employeeCode, string punchType, decimal latitude, decimal longitude,
        long? branchId, decimal? accuracy, string? deviceId, string? source)
    {
        if (!(Guid.TryParseExact(eventId, "D", out var id) || Guid.TryParseExact(eventId, "N", out id)) || id == Guid.Empty)
            throw new ArgumentException("A nonempty UUID EventId is required. Reuse the same ID when retrying the same punch.");
        static string Number(decimal value) => value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
        var hash = LeaveMovementKey.Hash(JsonSerializer.Serialize(new { employeeCode, punchType, latitude = Number(latitude), longitude = Number(longitude), branchId,
            accuracy = accuracy.HasValue ? Number(accuracy.Value) : null, deviceId,
            source = string.IsNullOrWhiteSpace(source) ? "MOBILE_APP" : source.Trim().ToUpperInvariant() }));
        return new(id.ToString("N"), hash);
    }
}
