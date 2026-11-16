using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Common;

public static class ShiftDefinitionHelper
{
    public static string Code(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50 || code.Trim() is "." or ".." || code.Any(c => char.IsControl(c) || c is '/' or '\\'))
            throw new ArgumentException("Shift code is required, up to 50 characters, without path separators or control characters.");
        return code.Trim();
    }

    public static string Version(string value)
    {
        var version = value?.Trim().Trim('"');
        if (version == null || version.Length != 64 || version.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("If-Match must contain one shift version from GET.");
        return version.ToUpperInvariant();
    }

    public static ShiftSchedule Build(string code, WriteShiftDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.NameLocal) || string.IsNullOrWhiteSpace(dto.NameEn) || dto.NameLocal.Trim().Length > 200 || dto.NameEn.Trim().Length > 200)
            throw new ArgumentException("Both shift names are required, up to 200 characters.");
        if (!dto.StartTime.HasValue || !dto.EndTime.HasValue || !dto.BreakMinutes.HasValue || dto.WorkingDays == null || !dto.IsActive.HasValue)
            throw new ArgumentException("Start, end, break, working days and active state must be supplied explicitly.");
        var shift = new ShiftSchedule
        {
            ShiftCode = code,
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            StartTime = dto.StartTime.Value,
            EndTime = dto.EndTime.Value,
            BreakMinutes = dto.BreakMinutes.Value,
            WorkingDaysJson = JsonSerializer.Serialize(dto.WorkingDays),
            IsActive = dto.IsActive.Value
        };
        ShiftDefinitionRules.GrossMinutes(shift);
        var days = ShiftDefinitionRules.WorkingDays(shift);
        shift.WorkingDaysJson = JsonSerializer.Serialize(days.OrderBy(d => d));
        return shift;
    }

    public static ShiftDefinitionDto Map(ShiftSchedule shift, bool used)
    {
        var errors = new List<string>();
        int[] days = Array.Empty<int>();
        try { days = ShiftDefinitionRules.WorkingDays(shift); } catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        try { ShiftDefinitionRules.GrossMinutes(shift); } catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        return new(shift.ShiftCode, shift.NameLocal, shift.NameEn, shift.StartTime, shift.EndTime, shift.BreakMinutes,
            days, shift.IsActive, used, ShiftDefinitionRules.Version(shift),
            DateTime.SpecifyKind(shift.CreationDate, DateTimeKind.Utc), shift.UpdateDate.HasValue ? DateTime.SpecifyKind(shift.UpdateDate.Value, DateTimeKind.Utc) : null,
            shift.WorkingDaysJson, errors);
    }
}
