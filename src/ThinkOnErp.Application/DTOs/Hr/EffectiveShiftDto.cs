namespace ThinkOnErp.Application.DTOs.Hr;

public record EffectiveShiftDto(string EmployeeCode, DateOnly WorkDate, string Status, string Source, string? ShiftCode,
    long? AssignmentId, long? DailyShiftId, DateTime? StartsAtLocal, DateTime? EndsAtLocal,
    int BreakMinutes, decimal ScheduledMinutes, decimal ScheduledHours);
