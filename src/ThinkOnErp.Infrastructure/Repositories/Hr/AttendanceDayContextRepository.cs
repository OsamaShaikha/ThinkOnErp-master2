using ThinkOnErp.Application.Services.Hr;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class AttendanceDayContextRepository(OracleDbContext db, IShiftTimingService? timing = null, IAttendanceTimeSettingsRepository? settings = null, IAttendanceCorrectionRepository? attendance = null) : IAttendanceDayContextRepository
{
    public async Task<bool> HasReviewDaysAsync(IReadOnlyList<string> employeeCodes, DateTime from, DateTime to, CancellationToken ct = default)
    {
        foreach (var chunk in employeeCodes.Distinct().Chunk(500))
        {
            if (await db.AttendanceDays.AsNoTracking().AnyAsync(d => chunk.Contains(d.EmployeeCode) && d.AttendanceDate >= from.Date && d.AttendanceDate <= to.Date && d.Status == "REVIEW_REQUIRED", ct)) return true;
            if (await db.RawAttendances.AsNoTracking().AnyAsync(r => chunk.Contains(r.EmployeeCode) && !r.IsProcessed && r.EventId != null &&
                r.ResolvedWorkDate.HasValue && r.ResolvedWorkDate.Value >= from.Date && r.ResolvedWorkDate.Value <= to.Date, ct)) return true;
            var lower = from.Date > DateTime.MinValue.AddDays(2) ? from.Date.AddDays(-2) : DateTime.MinValue;
            var upper = to.Date < DateTime.MaxValue.Date.AddDays(-3) ? to.Date.AddDays(3) : DateTime.MaxValue;
            var unbound = await db.RawAttendances.AsNoTracking().Where(r => chunk.Contains(r.EmployeeCode) && !r.IsProcessed && r.EventId != null &&
                !r.ResolvedWorkDate.HasValue && r.PunchTime >= lower && r.PunchTime < upper).Take(1001).ToListAsync(ct);
            if (unbound.Count > 1000) return true;
            foreach (var raw in unbound)
            {
                if (timing == null || settings == null || attendance == null) return true;
                try
                {
                    var setting = await settings.GetForEmployeeAsync(raw.EmployeeCode, ct);
                    var instant = new DateTimeOffset(DateTime.SpecifyKind(raw.PunchTime, DateTimeKind.Utc));
                    var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ShiftTimeRules.Zone(setting.TimeZoneId)).DateTime);
                    var schedule = await AttendanceWorkDateResolver.ResolveAsync(raw.EmployeeCode, instant, date, raw.PunchType, timing, attendance, ct);
                    var workDate = schedule.Shift.WorkDate.ToDateTime(TimeOnly.MinValue);
                    if (workDate >= from.Date && workDate <= to.Date) return true;
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException) { return true; }
            }
        }
        return false;
    }

    public async Task<AttendanceDayContext> GetAsync(string employeeCode, DateTime workDate, DateTime? startUtc, DateTime? endUtc, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct)
            ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var company = employee.BranchId.HasValue ? await db.SysBranches.Where(b => b.Id == employee.BranchId).Select(b => b.CompanyId).SingleOrDefaultAsync(ct) : (long?)db.HrScopeCompanyId;
        if (!company.HasValue || company <= 0 || db.HrScopeEnabled && company != db.HrScopeCompanyId) throw new InvalidOperationException("Employee company is outside the current HR scope.");
        var date = workDate.Date;
        var holiday = await db.PublicHolidays.AsNoTracking().AnyAsync(h => h.IsActive &&
            (!h.BranchId.HasValue || h.BranchId == employee.BranchId) &&
            (h.HolidayDate.Date == date || h.IsRecurring && h.HolidayDate.Month == date.Month && h.HolidayDate.Day == date.Day), ct);
        var leaves = await db.LeaveRequests.AsNoTracking().Where(l => l.EmployeeCode == employeeCode && l.Status == "APPROVED" && l.StartDate.Date <= date && l.EndDate.Date >= date).ToListAsync(ct);
        var online = startUtc.HasValue && endUtc.HasValue && await db.RawAttendances.AsNoTracking().AnyAsync(r => r.EmployeeCode == employeeCode &&
            r.IsProcessed && r.ProcessingOutcome != "IGNORED" && r.ProcessingOutcome != "REJECTED" && r.Source == "ONLINE" && r.PunchTime >= startUtc && r.PunchTime <= endUtc, ct);
        var pending = await db.RawAttendances.AsNoTracking().AnyAsync(r => r.EmployeeCode == employeeCode && !r.IsProcessed && r.EventId != null && r.ResolvedWorkDate == date, ct);
        if (!pending && startUtc.HasValue && endUtc.HasValue)
            pending = await db.RawAttendances.AsNoTracking().AnyAsync(r => r.EmployeeCode == employeeCode && !r.IsProcessed && r.EventId != null && !r.ResolvedWorkDate.HasValue &&
                r.PunchTime >= startUtc.Value && r.PunchTime <= endUtc.Value, ct);
        return new(holiday, leaves, online, pending);
    }
}
