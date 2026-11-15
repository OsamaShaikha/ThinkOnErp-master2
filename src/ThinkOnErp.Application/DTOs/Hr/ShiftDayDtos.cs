namespace ThinkOnErp.Application.DTOs.Hr;

public record WriteShiftDayDto(DateOnly? WorkDate, string? ShiftCode, bool? IsRestDay, bool? IsActive, string? ShiftVersion);
public record ShiftDayDto(long Id, string EmployeeCode, DateOnly WorkDate, string? ShiftCode, bool IsRestDay,
    bool IsActive, string Source, string Version, string? ShiftVersion, bool? ShiftIsActive,
    string CreationUser, DateTime CreationDate, string? UpdateUser, DateTime? UpdateDate);
public record ShiftDayPageDto(IReadOnlyList<ShiftDayDto> Items, int TotalCount, int Page, int Size);
