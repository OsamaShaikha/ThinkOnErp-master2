namespace ThinkOnErp.Application.DTOs.Hr;

public record WriteShiftAssignmentDto(string ShiftCode, string AssignmentKind, DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo, bool? IsActive, string ShiftVersion);
public record ShiftAssignmentDto(long Id, string EmployeeCode, string ShiftCode, string? AssignmentKind,
    string EffectiveKind, bool IsLegacy, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive,
    string Version, string? ShiftVersion, bool? ShiftIsActive, DateTime CreationDate, DateTime? UpdateDate);
public record ShiftAssignmentPageDto(IReadOnlyList<ShiftAssignmentDto> Items, int TotalCount, int Page, int Size);
