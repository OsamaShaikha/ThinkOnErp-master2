using System;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Common;

public static class ShiftAssignmentHelper
{
    public static EmployeeShiftAssignment Build(string employeeCode, WriteShiftAssignmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var kind = dto.AssignmentKind?.Trim().ToUpperInvariant();
        if (kind is not ("BASE" or "TEMPORARY")) throw new ArgumentException("AssignmentKind must explicitly be BASE or TEMPORARY.");
        if (!dto.EffectiveFrom.HasValue || !dto.IsActive.HasValue || string.IsNullOrWhiteSpace(dto.ShiftCode) || dto.ShiftCode.Trim().Length > 50)
            throw new ArgumentException("Shift code, start date and active state are required.");
        if (kind == "TEMPORARY" && !dto.EffectiveTo.HasValue || dto.EffectiveTo.HasValue && dto.EffectiveTo.Value < dto.EffectiveFrom.Value)
            throw new ArgumentException("Temporary assignments need an end date and dates must be ordered.");
        return new EmployeeShiftAssignment
        {
            EmployeeCode = employeeCode,
            ShiftCode = dto.ShiftCode.Trim(),
            AssignmentKind = kind,
            EffectiveFrom = dto.EffectiveFrom.Value.ToDateTime(TimeOnly.MinValue),
            EffectiveTo = dto.EffectiveTo?.ToDateTime(TimeOnly.MinValue),
            IsActive = dto.IsActive.Value
        };
    }

    public static ShiftAssignmentDto Map(EmployeeShiftAssignment a, ShiftSchedule? shift)
        => new(a.Id, a.EmployeeCode, a.ShiftCode, a.AssignmentKind, ShiftAssignmentRules.Kind(a), a.AssignmentKind == null,
            DateOnly.FromDateTime(a.EffectiveFrom), a.EffectiveTo.HasValue ? DateOnly.FromDateTime(a.EffectiveTo.Value) : null, a.IsActive,
            ShiftAssignmentRules.Version(a), shift == null ? null : ShiftDefinitionRules.Version(shift), shift?.IsActive,
            DateTime.SpecifyKind(a.CreationDate, DateTimeKind.Utc), a.UpdateDate.HasValue ? DateTime.SpecifyKind(a.UpdateDate.Value, DateTimeKind.Utc) : null);
}
