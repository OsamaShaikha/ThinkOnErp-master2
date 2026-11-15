using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public record CreateWorkCalendarDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsDefault,
    List<WorkCalendarDayDto>? Days
);

public record WorkCalendarDayDto(
    int DayOfWeek,
    bool IsWorkingDay,
    decimal StandardWorkingHours,
    string? DefaultShiftCode
);

public record CreatePublicHolidayDto(
    long CompanyId,
    long? BranchId,
    string NameAr,
    string NameEn,
    DateTime HolidayDate,
    bool IsPaid,
    bool IsRecurring,
    string? Description
);
