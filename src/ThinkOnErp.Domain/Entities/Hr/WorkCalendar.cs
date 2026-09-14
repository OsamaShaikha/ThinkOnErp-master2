using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class WorkCalendar
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<WorkCalendarDay> Days { get; set; } = new();
}

public sealed class WorkCalendarDay
{
    public long Id { get; set; }
    public long WorkCalendarId { get; set; }
    public int DayOfWeek { get; set; } // 0 = Sunday .. 6 = Saturday
    public bool IsWorkingDay { get; set; }
    public decimal StandardWorkingHours { get; set; } = 8.00m;
    public string? DefaultShiftCode { get; set; }

    public WorkCalendar? WorkCalendar { get; set; }
}

public sealed class PublicHoliday
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsRecurring { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
