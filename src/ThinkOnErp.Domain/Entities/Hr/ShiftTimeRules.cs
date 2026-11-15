namespace ThinkOnErp.Domain.Entities.Hr;

public record ShiftUtcWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc, decimal ElapsedMinutes);

public static class ShiftTimeRules
{
    public static DateTimeOffset ParseInstant(string value)
    {
        var text = value?.Trim();
        var hasOffset = text != null && (text.EndsWith('Z') || text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':');
        if (!hasOffset || !text!.Contains('T') || !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var instant))
            throw new ArgumentException("Punch instant must be ISO 8601 with Z or an explicit UTC offset.");
        return instant;
    }

    public static TimeZoneInfo Zone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId) || zoneId.Length > 100) throw new ArgumentException("An explicit company time zone is required.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(zoneId.Trim()); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Unknown or invalid time zone.", nameof(zoneId), ex); }
    }

    public static ShiftUtcWindow ToUtc(DateTime startLocal, DateTime endLocal, TimeZoneInfo zone)
    {
        var start = ConvertBoundary(startLocal, zone); var end = ConvertBoundary(endLocal, zone);
        if (end <= start) throw new InvalidOperationException("Shift UTC end must follow its start.");
        return new(start, end, (decimal)(end - start).Ticks / TimeSpan.TicksPerMinute);
    }

    private static DateTimeOffset ConvertBoundary(DateTime local, TimeZoneInfo zone)
    {
        if (local.Kind != DateTimeKind.Unspecified) throw new ArgumentException("Shift boundaries must be local clock values without a UTC/local kind.");
        if (zone.IsInvalidTime(local)) throw new InvalidOperationException("Shift boundary falls in a daylight-saving gap. Review the daily roster.");
        if (zone.IsAmbiguousTime(local)) throw new InvalidOperationException("Shift boundary has two UTC meanings during daylight-saving overlap. Review the daily roster.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    public static bool Contains(ShiftUtcWindow window, DateTimeOffset instant, string punchType)
        => punchType switch
        {
            "IN" => instant >= window.StartUtc && instant < window.EndUtc,
            "OUT" => instant > window.StartUtc && instant <= window.EndUtc,
            _ => throw new ArgumentException("Punch type must be IN or OUT.")
        };
}
