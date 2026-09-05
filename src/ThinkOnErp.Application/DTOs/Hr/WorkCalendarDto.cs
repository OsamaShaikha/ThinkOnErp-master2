using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class WorkCalendarDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public List<WorkCalendarDayDto> Days { get; set; } = new();
}

public sealed class WorkCalendarDayDto
{
    public long Id { get; set; }
    public long WorkCalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public string? DefaultShiftCode { get; set; }
    public decimal StandardWorkingHours { get; set; }
}

public sealed class CreateWorkCalendarDto
{
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public List<CreateWorkCalendarDayDto> Days { get; set; } = new();
}

public sealed class CreateWorkCalendarDayDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public string? DefaultShiftCode { get; set; }
    public decimal StandardWorkingHours { get; set; } = 8.0m;
}


public sealed class PublicHolidayDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public bool IsPaid { get; set; }
    public bool IsRecurring { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreatePublicHolidayDto
{
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsRecurring { get; set; }
    public string? Description { get; set; }
}
