using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        ILogger<AttendanceService> logger)
    {
        _attendanceRepository = attendanceRepository ?? throw new ArgumentNullException(nameof(attendanceRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ShiftScheduleDto>> GetAllShiftsAsync(bool activeOnly = true)
    {
        var shifts = await _attendanceRepository.GetAllShiftsAsync(activeOnly);
        return shifts.Select(MapToShiftDto).ToList();
    }

    public async Task<ShiftScheduleDto?> GetShiftByCodeAsync(string shiftCode)
    {
        var shift = await _attendanceRepository.GetShiftByCodeAsync(shiftCode);
        return shift == null ? null : MapToShiftDto(shift);
    }

    public async Task<ShiftScheduleDto> CreateShiftAsync(CreateShiftScheduleDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.ShiftCode.Trim().ToUpperInvariant();

        if (await _attendanceRepository.ShiftCodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز الوردية/الدوام ({code}) موجود مسبقاً.", "SHIFT_CODE_DUPLICATE");
        }

        var workingDays = dto.WorkingDays ?? new List<string> { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" };

        var shift = new ShiftSchedule
        {
            ShiftCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            BreakMinutes = dto.BreakMinutes,
            WorkingDaysJson = JsonSerializer.Serialize(workingDays),
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _attendanceRepository.AddShiftAsync(shift);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Created shift schedule {Code} by {User}", code, currentUser);
        return MapToShiftDto(shift);
    }

    public async Task<ShiftScheduleDto> UpdateShiftAsync(string shiftCode, CreateShiftScheduleDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var shift = await _attendanceRepository.GetShiftByCodeAsync(shiftCode);
        if (shift == null)
        {
            throw new HrNotFoundException($"الوردية ({shiftCode}) غير موجودة.", "SHIFT_NOT_FOUND");
        }

        var workingDays = dto.WorkingDays ?? new List<string> { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" };

        shift.NameAr = dto.NameAr.Trim();
        shift.NameEn = dto.NameEn.Trim();
        shift.StartTime = dto.StartTime;
        shift.EndTime = dto.EndTime;
        shift.BreakMinutes = dto.BreakMinutes;
        shift.WorkingDaysJson = JsonSerializer.Serialize(workingDays);
        shift.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        shift.UpdateDate = DateTime.UtcNow;

        _attendanceRepository.UpdateShift(shift);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Updated shift schedule {Code} by {User}", shiftCode, currentUser);
        return MapToShiftDto(shift);
    }

    public async Task AssignShiftToEmployeeAsync(AssignShiftDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var emp = await _employeeRepository.GetByCodeAsync(dto.EmployeeCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({dto.EmployeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var shift = await _attendanceRepository.GetShiftByCodeAsync(dto.ShiftCode);
        if (shift == null)
        {
            throw new HrNotFoundException($"الوردية ({dto.ShiftCode}) غير موجودة.", "SHIFT_NOT_FOUND");
        }

        var assignment = new EmployeeShiftAssignment
        {
            EmployeeCode = dto.EmployeeCode.Trim().ToUpperInvariant(),
            ShiftCode = dto.ShiftCode.Trim().ToUpperInvariant(),
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _attendanceRepository.AddShiftAssignmentAsync(assignment);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Assigned shift {Shift} to employee {Emp}", dto.ShiftCode, dto.EmployeeCode);
    }

    public async Task<AttendanceRecordDto> ClockInAsync(ClockInRequestDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        // Check idempotency if key supplied
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existingWithKey = await _attendanceRepository.GetAttendanceByIdempotencyKeyAsync(dto.IdempotencyKey);
            if (existingWithKey != null)
            {
                return MapToAttendanceDto(existingWithKey);
            }
        }

        var employee = await _employeeRepository.GetByCodeAsync(empCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var punchTime = dto.Timestamp ?? DateTime.UtcNow;
        var today = punchTime.Date;

        var existingRecord = await _attendanceRepository.GetAttendanceByEmployeeAndDateAsync(empCode, today);
        if (existingRecord != null && existingRecord.ClockIn.HasValue)
        {
            throw new HrConflictException($"تم تسجيل حركة الدخول مسبقاً للموظف ({empCode}) بتاريخ اليوم ({today:yyyy-MM-dd}).", "ALREADY_CLOCKED_IN");
        }

        // Compare against assigned shift
        var shiftAssignment = await _attendanceRepository.GetActiveShiftAssignmentAsync(empCode, today);
        var lateMinutes = 0;
        var status = "ON_TIME";

        if (shiftAssignment?.ShiftSchedule != null)
        {
            var scheduledStartTime = today.Add(shiftAssignment.ShiftSchedule.StartTime);
            if (punchTime > scheduledStartTime.AddMinutes(5)) // 5 minute grace period
            {
                lateMinutes = (int)(punchTime - scheduledStartTime).TotalMinutes;
                status = "LATE";
            }
        }

        if (existingRecord != null)
        {
            existingRecord.ClockIn = punchTime;
            existingRecord.Source = dto.Source;
            existingRecord.Status = status;
            existingRecord.LateMinutes = lateMinutes;
            existingRecord.IdempotencyKey = dto.IdempotencyKey;
            existingRecord.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
            existingRecord.UpdateDate = DateTime.UtcNow;
            _attendanceRepository.UpdateAttendance(existingRecord);
            await _attendanceRepository.SaveChangesAsync();
            return MapToAttendanceDto(existingRecord);
        }

        var record = new AttendanceRecord
        {
            EmployeeCode = empCode,
            AttendanceDate = today,
            ClockIn = punchTime,
            Source = dto.Source,
            Status = status,
            LateMinutes = lateMinutes,
            IdempotencyKey = dto.IdempotencyKey,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _attendanceRepository.AddAttendanceAsync(record);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Clock-in recorded for {Emp} at {Time} (Status: {Status})", empCode, punchTime, status);
        return MapToAttendanceDto(record);
    }

    public async Task<AttendanceRecordDto> ClockOutAsync(ClockOutRequestDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var punchTime = dto.Timestamp ?? DateTime.UtcNow;
        var today = punchTime.Date;

        var record = await _attendanceRepository.GetAttendanceByEmployeeAndDateAsync(empCode, today);
        if (record == null || !record.ClockIn.HasValue)
        {
            throw new HrValidationException($"لم يتم تسجيل حركة دخول مسبقة للموظف ({empCode}) لتسجيل الخروج.", "NO_CLOCK_IN_RECORDED");
        }

        record.ClockOut = punchTime;

        // Calculate hours
        var totalHours = (decimal)(punchTime - record.ClockIn.Value).TotalHours;
        record.TotalWorkHours = Math.Max(0, Math.Round(totalHours, 2));

        // Check early leave
        var shiftAssignment = await _attendanceRepository.GetActiveShiftAssignmentAsync(empCode, today);
        if (shiftAssignment?.ShiftSchedule != null)
        {
            var scheduledEndTime = today.Add(shiftAssignment.ShiftSchedule.EndTime);
            if (punchTime < scheduledEndTime)
            {
                record.EarlyLeaveMinutes = (int)(scheduledEndTime - punchTime).TotalMinutes;
                if (record.Status == "ON_TIME")
                {
                    record.Status = "EARLY_LEAVE";
                }
            }
        }

        record.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        record.UpdateDate = DateTime.UtcNow;

        _attendanceRepository.UpdateAttendance(record);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Clock-out recorded for {Emp} at {Time} (Hours: {Hours})", empCode, punchTime, record.TotalWorkHours);
        return MapToAttendanceDto(record);
    }

    public async Task<List<AttendanceRecordDto>> GetAttendanceRecordsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null)
    {
        var records = await _attendanceRepository.GetAttendanceAsync(employeeCode, fromDate, toDate, status);
        return records.Select(MapToAttendanceDto).ToList();
    }

    public async Task<AttendanceRecordDto> CorrectAttendanceAsync(long id, CorrectAttendanceDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.CorrectionReason))
        {
            throw new HrValidationException("سبب التعديل إلزامي لتوثيق الحركة الرقابية.", "CORRECTION_REASON_REQUIRED");
        }

        var record = await _attendanceRepository.GetAttendanceByIdAsync(id);
        if (record == null)
        {
            throw new HrNotFoundException($"سجل الحضور رقم ({id}) غير موجود.", "ATTENDANCE_RECORD_NOT_FOUND");
        }

        record.ClockIn = dto.ClockIn;
        record.ClockOut = dto.ClockOut;
        record.Status = dto.Status;
        record.CorrectedBy = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        record.CorrectionReason = dto.CorrectionReason.Trim();
        record.UpdateUser = record.CorrectedBy;
        record.UpdateDate = DateTime.UtcNow;

        if (record.ClockIn.HasValue && record.ClockOut.HasValue)
        {
            var hours = (decimal)(record.ClockOut.Value - record.ClockIn.Value).TotalHours;
            record.TotalWorkHours = Math.Max(0, Math.Round(hours, 2));
        }

        _attendanceRepository.UpdateAttendance(record);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Attendance record {Id} corrected by {User}: {Reason}", id, record.CorrectedBy, record.CorrectionReason);
        return MapToAttendanceDto(record);
    }

    public async Task<List<OvertimeRecordDto>> GetOvertimeRecordsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null)
    {
        var records = await _attendanceRepository.GetOvertimeAsync(employeeCode, fromDate, toDate, status);
        return records.Select(MapToOvertimeDto).ToList();
    }

    public async Task<OvertimeRecordDto> RequestOvertimeAsync(RequestOvertimeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var overtime = new OvertimeRecord
        {
            EmployeeCode = empCode,
            OvertimeDate = dto.OvertimeDate.Date,
            Hours = dto.Hours,
            RateMultiplier = dto.RateMultiplier > 0 ? dto.RateMultiplier : 1.25m,
            Status = "PENDING",
            Reason = dto.Reason,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _attendanceRepository.AddOvertimeAsync(overtime);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Requested overtime for {Emp} on {Date} ({Hours} hrs)", empCode, dto.OvertimeDate, dto.Hours);
        return MapToOvertimeDto(overtime);
    }

    public async Task<OvertimeRecordDto> ApproveOvertimeAsync(long id, string approvedBy)
    {
        var overtime = await _attendanceRepository.GetOvertimeByIdAsync(id);
        if (overtime == null)
        {
            throw new HrNotFoundException($"طلب العمل الإضافي رقم ({id}) غير موجود.", "OVERTIME_NOT_FOUND");
        }

        overtime.Status = "APPROVED";
        overtime.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        overtime.ApprovalDate = DateTime.UtcNow;
        overtime.UpdateUser = overtime.ApprovedBy;
        overtime.UpdateDate = DateTime.UtcNow;

        _attendanceRepository.UpdateOvertime(overtime);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Approved overtime {Id} by {User}", id, approvedBy);
        return MapToOvertimeDto(overtime);
    }

    public async Task<OvertimeRecordDto> RejectOvertimeAsync(long id, string rejectedBy)
    {
        var overtime = await _attendanceRepository.GetOvertimeByIdAsync(id);
        if (overtime == null)
        {
            throw new HrNotFoundException($"طلب العمل الإضافي رقم ({id}) غير موجود.", "OVERTIME_NOT_FOUND");
        }

        overtime.Status = "REJECTED";
        overtime.ApprovedBy = string.IsNullOrWhiteSpace(rejectedBy) ? "SYSTEM" : rejectedBy;
        overtime.ApprovalDate = DateTime.UtcNow;
        overtime.UpdateUser = overtime.ApprovedBy;
        overtime.UpdateDate = DateTime.UtcNow;

        _attendanceRepository.UpdateOvertime(overtime);
        await _attendanceRepository.SaveChangesAsync();

        _logger.LogInformation("Rejected overtime {Id} by {User}", id, rejectedBy);
        return MapToOvertimeDto(overtime);
    }

    private static ShiftScheduleDto MapToShiftDto(ShiftSchedule s)
    {
        return new ShiftScheduleDto
        {
            ShiftCode = s.ShiftCode,
            NameAr = s.NameAr,
            NameEn = s.NameEn,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            BreakMinutes = s.BreakMinutes,
            WorkingDaysJson = s.WorkingDaysJson,
            IsActive = s.IsActive,
            CreationDate = s.CreationDate
        };
    }

    private static AttendanceRecordDto MapToAttendanceDto(AttendanceRecord a)
    {
        return new AttendanceRecordDto
        {
            Id = a.Id,
            EmployeeCode = a.EmployeeCode,
            EmployeeNameAr = a.Employee?.NameAr ?? string.Empty,
            EmployeeNameEn = a.Employee?.NameEn ?? string.Empty,
            DepartmentName = a.Employee?.Department?.NameEn,
            AttendanceDate = a.AttendanceDate,
            ClockIn = a.ClockIn,
            ClockOut = a.ClockOut,
            Source = a.Source,
            Status = a.Status,
            LateMinutes = a.LateMinutes,
            EarlyLeaveMinutes = a.EarlyLeaveMinutes,
            TotalWorkHours = a.TotalWorkHours,
            CorrectedBy = a.CorrectedBy,
            CorrectionReason = a.CorrectionReason
        };
    }

    private static OvertimeRecordDto MapToOvertimeDto(OvertimeRecord o)
    {
        return new OvertimeRecordDto
        {
            Id = o.Id,
            EmployeeCode = o.EmployeeCode,
            EmployeeNameEn = o.Employee?.NameEn ?? string.Empty,
            OvertimeDate = o.OvertimeDate,
            Hours = o.Hours,
            RateMultiplier = o.RateMultiplier,
            Status = o.Status,
            Reason = o.Reason,
            ApprovedBy = o.ApprovedBy,
            ApprovalDate = o.ApprovalDate
        };
    }
}
