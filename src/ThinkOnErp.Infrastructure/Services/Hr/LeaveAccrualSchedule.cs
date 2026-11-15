namespace ThinkOnErp.Infrastructure.Services.Hr;

public readonly record struct LeaveAccrualRunDates(
    DateOnly LocalDate,
    DateOnly LastCompletedLocalDate,
    DateOnly PreviousMonthEnd);

public static class LeaveAccrualSchedule
{
    public static LeaveAccrualRunDates Resolve(DateTime utcNow, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The scheduler clock value must be UTC.", nameof(utcNow));

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));
        var firstOfLocalMonth = new DateTime(localDate.Year, localDate.Month, 1);
        return new LeaveAccrualRunDates(localDate, localDate.AddDays(-1), DateOnly.FromDateTime(firstOfLocalMonth.AddDays(-1)));
    }
}
