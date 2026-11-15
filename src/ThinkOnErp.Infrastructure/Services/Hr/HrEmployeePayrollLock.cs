using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeePayrollLock
{
    public static async Task AcquireAsync(OracleDbContext db, string employeeCode, IReadOnlyList<HrDateRange> ranges, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Employee date locking requires a transaction.");
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct)
            ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var companyId = employee.BranchId.HasValue
            ? await db.SysBranches.Where(b => b.Id == employee.BranchId).Select(b => b.CompanyId).SingleOrDefaultAsync(ct)
            : db.HrScopeCompanyId > 0 ? db.HrScopeCompanyId : (long?)null;
        if (!companyId.HasValue || companyId.Value <= 0)
            throw new InvalidOperationException("Employee company must be resolved to a valid company before updating payroll inputs.");
        if (db.HrScopeEnabled && db.HrScopeCompanyId > 0 && db.HrScopeCompanyId != companyId.Value)
            throw new InvalidOperationException("Employee company is outside the current HR scope.");
        if (ranges.Any(r => r.End < r.Start)) throw new ArgumentException("Invalid employee date range.");
        if (ranges.Count > 0)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT STATUS,START_DATE,END_DATE FROM HR_PAYROLL_PERIOD WHERE TRUNC(START_DATE)<=:endDate AND TRUNC(END_DATE)>=:startDate ORDER BY ID FOR UPDATE";
            void Add(string name, object value) { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p); }
            Add("endDate", ranges.Max(r => r.End)); Add("startDate", ranges.Min(r => r.Start));
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                if (reader.GetString(0).Equals("CLOSED", StringComparison.OrdinalIgnoreCase) &&
                    ranges.Any(r => ShiftAssignmentRules.Overlap(r, new(reader.GetDateTime(1).Date, reader.GetDateTime(2).Date))))
                    throw new InvalidOperationException("This change overlaps a closed payroll period. Reopen it through the payroll workflow first.");
        }
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:employeeCode FOR UPDATE";
            var parameter = command.CreateParameter(); parameter.ParameterName = "employeeCode"; parameter.Value = employeeCode; command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(ct) == null) throw new KeyNotFoundException("Employee not found.");
        }
        var current = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct);
        if (current == null || current.BranchId != employee.BranchId)
            throw new InvalidOperationException("Employee scope changed while acquiring locks. Retry the operation.");
        foreach (var entry in db.ChangeTracker.Entries<Employee>().Where(e => e.Entity.EmployeeCode == employeeCode && e.State == EntityState.Unchanged).ToArray())
            await entry.ReloadAsync(ct);
        if (ranges.Count == 0) return;
        var from = ranges.Min(r => r.Start); var to = ranges.Max(r => r.End);
        var periods = await db.PayrollPeriods.AsNoTracking().Where(p => p.StartDate.Date <= to && p.EndDate.Date >= from)
            .Select(p => new { p.PeriodCode, p.StartDate, p.EndDate }).ToListAsync(ct);
        var codes = periods.Where(p => ranges.Any(r => ShiftAssignmentRules.Overlap(r, new(p.StartDate.Date, p.EndDate.Date)))).Select(p => p.PeriodCode).Distinct().ToArray();
        foreach (var chunk in codes.Chunk(500))
            // Integrity checks cover this employee's historical runs in other branches too.
            if (await db.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => chunk.Contains(r.PayPeriod) && (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID") &&
                r.Lines.Any(l => l.EmployeeCode == employeeCode), ct))
                throw new InvalidOperationException("This change affects approved payroll. Reverse its dependent payroll before changing inputs.");
    }
}
