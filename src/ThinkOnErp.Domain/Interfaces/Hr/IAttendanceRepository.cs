using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAttendanceRepository
{
    // Shift schedules
    Task<IReadOnlyList<ShiftSchedule>> GetAllShiftsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<ShiftSchedule?> GetShiftByCodeAsync(string shiftCode, CancellationToken cancellationToken = default);
    Task<bool> ShiftCodeExistsAsync(string shiftCode, CancellationToken cancellationToken = default);
    Task AddShiftAsync(ShiftSchedule shift, CancellationToken cancellationToken = default);
    void UpdateShift(ShiftSchedule shift);

    // Employee Shift Assignment
    Task<EmployeeShiftAssignment?> GetActiveShiftAssignmentAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task AddShiftAssignmentAsync(EmployeeShiftAssignment assignment, CancellationToken cancellationToken = default);

    // Attendance Records
    Task<IReadOnlyList<AttendanceRecord>> GetAttendanceAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetAttendanceByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetAttendanceByEmployeeAndDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetAttendanceByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAttendanceAsync(AttendanceRecord record, CancellationToken cancellationToken = default);
    void UpdateAttendance(AttendanceRecord record);

    // Overtime
    Task<IReadOnlyList<OvertimeRecord>> GetOvertimeAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<OvertimeRecord?> GetOvertimeByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddOvertimeAsync(OvertimeRecord record, CancellationToken cancellationToken = default);
    void UpdateOvertime(OvertimeRecord record);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
