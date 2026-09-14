using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceCalculationEngine
{
    Task<RawAttendance> IngestPunchAsync(string employeeCode, DateTime punchTime, string punchType, string? deviceId, string source = "BIOMETRIC", CancellationToken cancellationToken = default);
    Task<AttendanceDay> ProcessEmployeeDayAttendanceAsync(string employeeCode, DateTime date, DateTime? checkIn, DateTime? checkOut, AttendancePolicy policy, decimal scheduledHours = 8.0m, TimeSpan? shiftStart = null, TimeSpan? shiftEnd = null, CancellationToken cancellationToken = default);
    string ComputePunchHash(string employeeCode, DateTime punchTime, string punchType);
}

public sealed class AttendanceCalculationEngine : IAttendanceCalculationEngine
{
    private readonly IAttendanceCorrectionRepository _repository;

    public AttendanceCalculationEngine(IAttendanceCorrectionRepository repository)
    {
        _repository = repository;
    }

    public string ComputePunchHash(string employeeCode, DateTime punchTime, string punchType)
    {
        var rawString = $"{employeeCode.Trim().ToUpperInvariant()}_{punchTime:yyyyMMddHHmmss}_{punchType.Trim().ToUpperInvariant()}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        return Convert.ToHexString(bytes);
    }

    public async Task<RawAttendance> IngestPunchAsync(
        string employeeCode,
        DateTime punchTime,
        string punchType,
        string? deviceId,
        string source = "BIOMETRIC",
        CancellationToken cancellationToken = default)
    {
        var hash = ComputePunchHash(employeeCode, punchTime, punchType);

        var raw = new RawAttendance
        {
            EmployeeCode = employeeCode,
            PunchTime = punchTime,
            PunchType = punchType,
            DeviceId = deviceId,
            ExternalReference = hash,
            Source = source,
            IsProcessed = false,
            CreationUser = "BIOMETRIC_SYNC",
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddRawAttendanceAsync(raw, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public Task<AttendanceDay> ProcessEmployeeDayAttendanceAsync(
        string employeeCode,
        DateTime date,
        DateTime? checkIn,
        DateTime? checkOut,
        AttendancePolicy policy,
        decimal scheduledHours = 8.0m,
        TimeSpan? shiftStart = null,
        TimeSpan? shiftEnd = null,
        CancellationToken cancellationToken = default)
    {
        var attendanceDay = new AttendanceDay
        {
            EmployeeCode = employeeCode,
            AttendanceDate = date.Date,
            ScheduledHours = scheduledHours,
            FirstCheckIn = checkIn,
            LastCheckOut = checkOut,
            CreationUser = "ATTENDANCE_ENGINE",
            CreationDate = DateTime.UtcNow
        };

        // Missing Punch detection
        if (checkIn.HasValue && !checkOut.HasValue || !checkIn.HasValue && checkOut.HasValue)
        {
            attendanceDay.HasMissingPunch = true;
            attendanceDay.Status = "INCOMPLETE";
        }
        else if (!checkIn.HasValue && !checkOut.HasValue)
        {
            attendanceDay.Status = "ABSENT";
            return Task.FromResult(attendanceDay);
        }
        else
        {
            attendanceDay.Status = "PRESENT";
        }

        // Calculate hours worked
        if (checkIn.HasValue && checkOut.HasValue)
        {
            var diff = checkOut.Value - checkIn.Value;
            if (diff.TotalHours < 0) // Overnight boundary
                diff = diff.Add(TimeSpan.FromHours(24));

            attendanceDay.ActualWorkedHours = Math.Round((decimal)diff.TotalHours, 2);

            // Shift comparisons if shift schedule is provided
            var targetStart = shiftStart ?? new TimeSpan(8, 0, 0); // Default 08:00
            var actualStart = checkIn.Value.TimeOfDay;

            if (actualStart > targetStart)
            {
                var lateSpan = actualStart - targetStart;
                var totalLateMinutes = (int)lateSpan.TotalMinutes;
                // Subtract policy Grace Period
                if (totalLateMinutes > policy.GracePeriodMinutes)
                {
                    attendanceDay.LateArrivalMinutes = policy.AutoDeductLateArrival
                        ? totalLateMinutes - policy.GracePeriodMinutes
                        : totalLateMinutes;
                }
            }

            var targetEnd = shiftEnd ?? new TimeSpan(17, 0, 0); // Default 17:00
            var actualEnd = checkOut.Value.TimeOfDay;

            if (actualEnd < targetEnd)
            {
                var earlySpan = targetEnd - actualEnd;
                var totalEarlyMinutes = (int)earlySpan.TotalMinutes;
                if (totalEarlyMinutes > policy.EarlyLeaveToleranceMinutes)
                {
                    attendanceDay.EarlyLeaveMinutes = totalEarlyMinutes;
                }
            }

            // Overtime evaluation
            if (attendanceDay.ActualWorkedHours > scheduledHours)
            {
                var excessHours = attendanceDay.ActualWorkedHours - scheduledHours;
                var excessMinutes = excessHours * 60;
                if (excessMinutes >= policy.MinMinutesForOvertime)
                {
                    attendanceDay.OvertimeHours = Math.Round(excessHours, 2);
                }
            }
        }

        return Task.FromResult(attendanceDay);
    }
}
