using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class AttendanceTimeSettingsRepository(OracleDbContext db) : IAttendanceTimeSettingsRepository
{
    private long Company => db.HrScopeCompanyId > 0 ? db.HrScopeCompanyId : throw new InvalidOperationException("A trusted company scope is required.");
    public Task<AttendanceTimeSettings?> GetCurrentAsync(CancellationToken ct = default)
    {
        var company = Company;
        return db.Set<AttendanceTimeSettings>().AsNoTracking().SingleOrDefaultAsync(s => s.CompanyId == company, ct);
    }

    public async Task<AttendanceTimeSettings> InitializeAsync(string timeZoneId, string user, CancellationToken ct = default)
    {
        var zone = ShiftTimeRules.Zone(timeZoneId); var company = Company;
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Time configuration requires ReadCommitted.");
        await using var owned = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT \"Id\" FROM SYS_BRANCH WHERE COMPANY_ID=:companyId ORDER BY \"Id\" FOR UPDATE";
            var p = command.CreateParameter(); p.ParameterName = "companyId"; p.Value = company; command.Parameters.Add(p);
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Company must have a branch before configuring attendance time.");
                while (await reader.ReadAsync(ct)) { }
            }
            var existing = await GetCurrentAsync(ct);
            if (existing != null)
            {
                if (existing.TimeZoneId != zone.Id) throw new InvalidOperationException("Company time zone is already configured. A dated migration is required to change it.");
                if (owned != null) await owned.CommitAsync(ct);
                return existing;
            }
            var setting = new AttendanceTimeSettings { CompanyId = company, TimeZoneId = zone.Id, CreationUser = user, CreationDate = DateTime.UtcNow };
            db.Add(setting); await db.SaveChangesAsync(ct); await db.Entry(setting).ReloadAsync(ct);
            if (owned != null) await owned.CommitAsync(ct); return setting;
        }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }

    public async Task<AttendanceTimeSettings> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct)
            ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var company = employee.BranchId.HasValue
            ? await db.SysBranches.Where(b => b.Id == employee.BranchId).Select(b => b.CompanyId).SingleOrDefaultAsync(ct) : (long?)Company;
        if (company == null || db.HrScopeEnabled && company != Company) throw new InvalidOperationException("Employee company is outside the current HR scope.");
        return await db.Set<AttendanceTimeSettings>().AsNoTracking().SingleOrDefaultAsync(s => s.CompanyId == company, ct)
            ?? throw new InvalidOperationException("Configure the company's attendance time zone first.");
    }
}
