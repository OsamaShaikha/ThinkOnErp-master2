using System;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Common;

public static class ShiftDayHelper
{
    public static string? DefinitionVersion(WriteShiftDayDto dto)
    {
        if (dto.IsRestDay == true)
        {
            if (dto.ShiftVersion != null) throw new ArgumentException("Rest entries cannot specify a shift version.");
            return null;
        }
        return ShiftAssignmentRules.ParseVersion(dto.ShiftVersion);
    }

    public static EmployeeShiftDay Build(string employeeCode, WriteShiftDayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!dto.WorkDate.HasValue || !dto.IsRestDay.HasValue || !dto.IsActive.HasValue)
            throw new ArgumentException("WorkDate, IsRestDay and IsActive are required explicitly.");
        var day = new EmployeeShiftDay
        {
            EmployeeCode = employeeCode,
            WorkDate = dto.WorkDate.Value.ToDateTime(TimeOnly.MinValue),
            ShiftCode = dto.ShiftCode?.Trim(),
            IsRestDay = dto.IsRestDay.Value,
            IsActive = dto.IsActive.Value,
            Source = "MANUAL"
        };
        ShiftDayRules.Validate(day);
        return day;
    }

    public static ShiftDayDto Map(EmployeeShiftDay d, ShiftSchedule? shift)
        => new(d.Id, d.EmployeeCode, DateOnly.FromDateTime(d.WorkDate),
            d.ShiftCode, d.IsRestDay, d.IsActive, d.Source, ShiftDayRules.Version(d),
            shift == null ? null : ShiftDefinitionRules.Version(shift), shift?.IsActive,
            d.CreationUser, DateTime.SpecifyKind(d.CreationDate, DateTimeKind.Utc), d.UpdateUser,
            d.UpdateDate.HasValue ? DateTime.SpecifyKind(d.UpdateDate.Value, DateTimeKind.Utc) : null);
}
