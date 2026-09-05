using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceService
{
    // Shifts
    Task<List<ShiftScheduleDto>> GetAllShiftsAsync(bool activeOnly = true);
    Task<ShiftScheduleDto?> GetShiftByCodeAsync(string shiftCode);
    Task<ShiftScheduleDto> CreateShiftAsync(CreateShiftScheduleDto dto, string currentUser);
    Task<ShiftScheduleDto> UpdateShiftAsync(string shiftCode, CreateShiftScheduleDto dto, string currentUser);
    Task AssignShiftToEmployeeAsync(AssignShiftDto dto, string currentUser);

    // Punches
    Task<AttendanceRecordDto> ClockInAsync(ClockInRequestDto dto, string currentUser);
    Task<AttendanceRecordDto> ClockOutAsync(ClockOutRequestDto dto, string currentUser);
    Task<List<AttendanceRecordDto>> GetAttendanceRecordsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null);

    Task<AttendanceRecordDto> CorrectAttendanceAsync(long id, CorrectAttendanceDto dto, string currentUser);

    // Overtime
    Task<List<OvertimeRecordDto>> GetOvertimeRecordsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null);

    Task<OvertimeRecordDto> RequestOvertimeAsync(RequestOvertimeDto dto, string currentUser);
    Task<OvertimeRecordDto> ApproveOvertimeAsync(long id, string approvedBy);
    Task<OvertimeRecordDto> RejectOvertimeAsync(long id, string rejectedBy);
}
