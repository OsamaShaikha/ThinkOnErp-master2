namespace ThinkOnErp.Application.DTOs.Hr;

public record WriteShiftDefinitionDto(string NameLocal, string NameEn, TimeSpan? StartTime, TimeSpan? EndTime,
    int? BreakMinutes, IReadOnlyList<int>? WorkingDays, bool? IsActive);
public record CreateShiftDefinitionDto(string ShiftCode, WriteShiftDefinitionDto Definition);
public record ShiftDefinitionDto(string ShiftCode, string NameLocal, string NameEn, TimeSpan StartTime, TimeSpan EndTime,
    int BreakMinutes, IReadOnlyList<int> WorkingDays, bool IsActive, bool IsInUse, string Version,
    DateTime CreationDate, DateTime? UpdateDate, string WorkingDaysJson, IReadOnlyList<string> ValidationErrors);
public record ShiftDefinitionPageDto(IReadOnlyList<ShiftDefinitionDto> Items, int TotalCount, int Page, int Size);
