using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class AttendanceCalculationEngine : IAttendanceCalculationEngine
{
    private readonly IAttendanceCorrectionRepository _attendanceCorrectionRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IWorkCalendarRepository _workCalendarRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILogger<AttendanceCalculationEngine> _logger;

    public AttendanceCalculationEngine(
        IAttendanceCorrectionRepository attendanceCorrectionRepo,
        IAttendanceRepository attendanceRepo,
        IWorkCalendarRepository workCalendarRepo,
        IPolicyRepository policyRepo,
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        ILogger<AttendanceCalculationEngine> logger)
    {
        _attendanceCorrectionRepo = attendanceCorrectionRepo ?? throw new ArgumentNullException(nameof(attendanceCorrectionRepo));
        _attendanceRepo = attendanceRepo ?? throw new ArgumentNullException(nameof(attendanceRepo));
        _workCalendarRepo = workCalendarRepo ?? throw new ArgumentNullException(nameof(workCalendarRepo));
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _leaveRepo = leaveRepo ?? throw new ArgumentNullException(nameof(leaveRepo));
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AttendanceDay> CalculateDailyAttendanceAsync(string employeeCode, DateTime date, long companyId, string currentUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeCode);
        var targetDate = date.Date;

        var employee = await _employeeRepo.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        // 1. Load active Attendance Policy (with grace period & tolerance)
        var policy = await _policyRepo.GetEffectiveAttendancePolicyAsync(companyId, targetDate);
        var gracePeriod = policy?.GracePeriodMinutes ?? 15;
        var earlyTolerance = policy?.EarlyLeaveToleranceMinutes ?? 0;
        var minOvertimeMinutes = policy?.MinimumMinutesForOvertime ?? 30;

        // 2. Load Work Calendar & Public Holiday
        var holiday = await _workCalendarRepo.GetHolidayByDateAsync(companyId, targetDate);
        var calendar = await _workCalendarRepo.GetDefaultCalendarAsync(companyId, targetDate);
        var dayConfig = calendar?.Days.FirstOrDefault(d => d.DayOfWeek == targetDate.DayOfWeek);
        var isWorkingDay = holiday == null && (dayConfig?.IsWorkingDay ?? (targetDate.DayOfWeek != DayOfWeek.Friday && targetDate.DayOfWeek != DayOfWeek.Saturday));

        // 3. Load Shift
        var shiftAssignment = await _attendanceRepo.GetActiveShiftAssignmentAsync(employeeCode, targetDate);
        ShiftSchedule? shift = shiftAssignment?.ShiftSchedule;
        if (shift == null && !string.IsNullOrWhiteSpace(dayConfig?.DefaultShiftCode))
        {
            shift = await _attendanceRepo.GetShiftByCodeAsync(dayConfig.DefaultShiftCode);
        }

        var scheduledHours = 8.0m;
        if (shift != null)
        {
            var span = shift.EndTime > shift.StartTime
                ? (shift.EndTime - shift.StartTime).TotalHours
                : (shift.EndTime.Add(TimeSpan.FromHours(24)) - shift.StartTime).TotalHours;
            scheduledHours = Math.Max(0, (decimal)span - (shift.BreakMinutes / 60m));
        }
        else if (!isWorkingDay)
        {
            scheduledHours = 0m;
        }

        // 4. Check if Employee is on Approved Leave
        var leaves = await _leaveRepo.GetRequestsAsync(employeeCode: employeeCode, status: "APPROVED", fromDate: targetDate, toDate: targetDate);
        var activeLeave = leaves.FirstOrDefault();

        // 5. Load Raw Punches
        var punches = await _attendanceCorrectionRepo.GetRawPunchesByEmployeeAndDateAsync(employeeCode, targetDate);
        var firstIn = punches.Where(p => p.PunchType == "IN" || p.PunchType == "AUTO").OrderBy(p => p.PunchTime).FirstOrDefault()?.PunchTime;
        var lastOut = punches.Where(p => p.PunchType == "OUT").OrderByDescending(p => p.PunchTime).FirstOrDefault()?.PunchTime;

        // If no explicit OUT punch but multiple punches exist
        if (firstIn.HasValue && !lastOut.HasValue && punches.Count > 1)
        {
            lastOut = punches.OrderByDescending(p => p.PunchTime).First().PunchTime;
            if (lastOut == firstIn) lastOut = null;
        }

        var existingDay = await _attendanceCorrectionRepo.GetAttendanceDayAsync(employeeCode, targetDate);
        var day = existingDay ?? new AttendanceDay
        {
            EmployeeCode = employeeCode,
            AttendanceDate = targetDate,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        day.WorkCalendarId = calendar?.Id;
        day.ShiftCode = shift?.ShiftCode;
        day.ScheduledHours = scheduledHours;
        day.FirstCheckIn = firstIn;
        day.LastCheckOut = lastOut;
        day.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        day.UpdateDate = DateTime.UtcNow;

        // Determine Status & Metrics
        if (activeLeave != null)
        {
            day.Status = "ON_LEAVE";
            day.LeaveTypeCode = activeLeave.LeaveTypeCode;
            day.ActualWorkedHours = 0;
            day.LateArrivalMinutes = 0;
            day.EarlyLeaveMinutes = 0;
            day.OvertimeHours = 0;
            day.HasMissingPunch = false;
        }
        else if (holiday != null)
        {
            day.Status = "HOLIDAY";
            day.Notes = holiday.NameAr;
            day.ActualWorkedHours = 0;
            day.LateArrivalMinutes = 0;
            day.EarlyLeaveMinutes = 0;
            day.HasMissingPunch = false;

            // Holiday overtime if worked
            if (firstIn.HasValue && lastOut.HasValue)
            {
                var worked = (decimal)(lastOut.Value - firstIn.Value).TotalHours;
                day.ActualWorkedHours = Math.Max(0, Math.Round(worked, 2));
                day.OvertimeHours = day.ActualWorkedHours;
            }
        }
        else if (!isWorkingDay)
        {
            day.Status = "WEEKEND";
            day.ActualWorkedHours = 0;
            day.LateArrivalMinutes = 0;
            day.EarlyLeaveMinutes = 0;
            day.HasMissingPunch = false;

            // Weekend overtime if worked
            if (firstIn.HasValue && lastOut.HasValue)
            {
                var worked = (decimal)(lastOut.Value - firstIn.Value).TotalHours;
                day.ActualWorkedHours = Math.Max(0, Math.Round(worked, 2));
                day.OvertimeHours = day.ActualWorkedHours;
            }
        }
        else if (!firstIn.HasValue)
        {
            day.Status = "ABSENT";
            day.ActualWorkedHours = 0;
            day.LateArrivalMinutes = 0;
            day.EarlyLeaveMinutes = 0;
            day.OvertimeHours = 0;
            day.HasMissingPunch = false;
        }
        else if (firstIn.HasValue && !lastOut.HasValue)
        {
            day.Status = "MISSING_PUNCH";
            day.HasMissingPunch = true;
            day.ActualWorkedHours = 0;
            day.LateArrivalMinutes = 0;
            day.EarlyLeaveMinutes = 0;
            day.OvertimeHours = 0;
        }
        else // Present with both IN and OUT
        {
            day.HasMissingPunch = false;
            var worked = (decimal)(lastOut!.Value - firstIn!.Value).TotalHours;
            day.ActualWorkedHours = Math.Max(0, Math.Round(worked, 2));

            // Shift timings calculation
            int lateMinutes = 0;
            int earlyMinutes = 0;

            if (shift != null)
            {
                var scheduledStart = targetDate.Add(shift.StartTime);
                var isOvernight = shift.EndTime < shift.StartTime;
                var scheduledEnd = isOvernight ? targetDate.AddDays(1).Add(shift.EndTime) : targetDate.Add(shift.EndTime);

                // Late arrival check with grace period
                if (firstIn.Value > scheduledStart.AddMinutes(gracePeriod))
                {
                    lateMinutes = (int)(firstIn.Value - scheduledStart).TotalMinutes;
                }

                // Early departure check
                if (lastOut.Value < scheduledEnd.AddMinutes(-earlyTolerance))
                {
                    earlyMinutes = (int)(scheduledEnd - lastOut.Value).TotalMinutes;
                }
            }


            day.LateArrivalMinutes = Math.Max(0, lateMinutes);
            day.EarlyLeaveMinutes = Math.Max(0, earlyMinutes);

            // Overtime check
            if (day.ActualWorkedHours > scheduledHours && scheduledHours > 0)
            {
                var extraHours = day.ActualWorkedHours - scheduledHours;
                if ((extraHours * 60m) >= minOvertimeMinutes)
                {
                    day.OvertimeHours = Math.Round(extraHours, 2);
                }
            }

            if (day.LateArrivalMinutes > 0 && day.EarlyLeaveMinutes > 0)
            {
                day.Status = "LATE_AND_EARLY_LEAVE";
            }
            else if (day.LateArrivalMinutes > 0)
            {
                day.Status = "LATE";
            }
            else if (day.EarlyLeaveMinutes > 0)
            {
                day.Status = "EARLY_LEAVE";
            }
            else
            {
                day.Status = "PRESENT";
            }
        }

        if (existingDay == null)
        {
            await _attendanceCorrectionRepo.AddAttendanceDayAsync(day);
        }
        else
        {
            _attendanceCorrectionRepo.UpdateAttendanceDay(day);
        }

        await _attendanceCorrectionRepo.SaveChangesAsync();
        return day;
    }

    public async Task<int> ProcessUnprocessedRawPunchesAsync(DateTime date, long companyId, string currentUser)
    {
        var punches = await _attendanceCorrectionRepo.GetUnprocessedRawPunchesAsync(date);
        var employees = punches.Select(p => p.EmployeeCode).Distinct().ToList();

        var processed = 0;
        foreach (var empCode in employees)
        {
            await CalculateDailyAttendanceAsync(empCode, date, companyId, currentUser);
            processed++;
        }

        foreach (var p in punches)
        {
            p.IsProcessed = true;
            p.ProcessedDate = DateTime.UtcNow;
        }

        await _attendanceCorrectionRepo.SaveChangesAsync();
        return processed;
    }
}
