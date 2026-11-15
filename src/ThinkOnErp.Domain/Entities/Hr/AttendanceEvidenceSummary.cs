namespace ThinkOnErp.Domain.Entities.Hr;

public record AttendanceSession(DateTime CheckIn, DateTime? CheckOut, decimal DurationMinutes);

public record AttendanceEvidenceSummary(
    DateTime? FirstIn,
    DateTime? LastOut,
    bool NeedsReview,
    IReadOnlyList<AttendanceSession>? Sessions = null,
    decimal TotalSessionMinutes = 0m,
    bool IsCurrentlyInside = false,
    int TotalSessionsCount = 0,
    decimal TotalBreakGapMinutes = 0m)
{
    public static AttendanceEvidenceSummary Build(IEnumerable<RawAttendance> events, AttendanceDay? day, bool allowMultipleSessions = false)
    {
        List<(DateTime Time, string Type)> punches = events.Select(r => (Time: r.PunchTime, Type: r.PunchType)).ToList();
        if (day?.FirstCheckIn is DateTime first && !punches.Any(p => p.Type == "IN" && p.Time == first))
        {
            punches.Add((first, "IN"));
        }

        if (day?.LastCheckOut is DateTime last && !punches.Any(p => p.Type == "OUT" && p.Time == last))
        {
            punches.Add((last, "OUT"));
        }

        DateTime[] ins = punches.Where(p => p.Type == "IN").Select(p => p.Time).ToArray();
        DateTime[] outs = punches.Where(p => p.Type == "OUT").Select(p => p.Time).ToArray();
        DateTime? firstIn = ins.Length == 0 ? null : ins.Min();
        DateTime? lastOut = outs.Length == 0 ? null : outs.Max();

        List<(DateTime Time, string Type)> sortedPunches = punches.OrderBy(p => p.Time).ThenBy(p => p.Type, StringComparer.Ordinal).ToList();
        List<AttendanceSession> sessionsList = [];
        DateTime? currentIn = null;
        DateTime? previousOut = null;
        decimal totalBreakGapMinutes = 0m;
        bool isInside = false;

        foreach ((DateTime Time, string Type) punch in sortedPunches)
        {
            if (punch.Type == "IN")
            {
                if (currentIn == null)
                {
                    currentIn = punch.Time;
                    isInside = true;
                    if (previousOut.HasValue && punch.Time > previousOut.Value)
                    {
                        totalBreakGapMinutes += (decimal)(punch.Time - previousOut.Value).TotalMinutes;
                    }
                }
            }
            else if (punch.Type == "OUT")
            {
                if (currentIn.HasValue)
                {
                    decimal duration = (decimal)(punch.Time - currentIn.Value).TotalMinutes;
                    sessionsList.Add(new AttendanceSession(currentIn.Value, punch.Time, Math.Max(0m, duration)));
                    previousOut = punch.Time;
                    currentIn = null;
                    isInside = false;
                }
                else
                {
                    sessionsList.Add(new AttendanceSession(punch.Time, punch.Time, 0m));
                    previousOut = punch.Time;
                    isInside = false;
                }
            }
        }

        if (currentIn.HasValue)
        {
            sessionsList.Add(new AttendanceSession(currentIn.Value, null, 0m));
            isInside = true;
        }

        int sessionsCount = sessionsList.Count;
        decimal totalSessionMinutes = sessionsList.Sum(s => s.DurationMinutes);

        bool review = (!allowMultipleSessions && sessionsCount > 1) ||
                     (firstIn.HasValue && lastOut.HasValue && lastOut < firstIn) ||
                     punches.Any(p => p.Type is not ("IN" or "OUT")) ||
                     punches.GroupBy(p => p.Time.Ticks).Any(g => g.Select(p => p.Type).Distinct().Count() > 1);

        return new(firstIn, lastOut, review, sessionsList, totalSessionMinutes, isInside, sessionsCount, totalBreakGapMinutes);
    }
}
