using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrAttendanceTimeSettingsVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;
        await using var db = new OracleDbContext(options);
        var companies = await db.SysBranches.AsNoTracking()
            .Where(branch => branch.IsActive && branch.CompanyId.HasValue)
            .Select(branch => branch.CompanyId!.Value)
            .Distinct()
            .OrderBy(companyId => companyId)
            .ToListAsync();
        long? company = null;
        foreach (var candidate in companies)
        {
            if (!await db.Set<AttendanceTimeSettings>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(setting => setting.CompanyId == candidate) &&
                await db.Employees.AsNoTracking().AnyAsync(employee => employee.BranchId.HasValue &&
                    db.SysBranches.Any(branch => branch.Id == employee.BranchId && branch.CompanyId == candidate)))
            {
                company = candidate;
                break;
            }
        }
        if (!company.HasValue)
            throw new InvalidOperationException("Attendance time-settings verification requires a DEV_TEMPLATE company with an active employee/branch and no existing time-zone setting.");

        db.HrScopeCompanyId = company.Value;
        db.HrScopeEnabled = true;
        db.HrScopeAllBranches = true;
        var marker = "HR_TIME_SETTINGS_VERIFY_" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var repository = new AttendanceTimeSettingsRepository(db);
            var created = await repository.InitializeAsync("Asia/Amman", marker);
            if (created.CompanyId != company.Value || created.TimeZoneId != "Asia/Amman" || created.CreationUser != marker)
                throw new InvalidOperationException("Attendance time-settings initialization did not preserve the scoped company, time zone, and actor.");
            var employeeCode = await (from employee in db.Employees.AsNoTracking()
                join branch in db.SysBranches.AsNoTracking() on employee.BranchId equals (long?)branch.Id
                where branch.CompanyId == company.Value
                select employee.EmployeeCode).FirstAsync();
            var forEmployee = await repository.GetForEmployeeAsync(employeeCode);
            if (forEmployee.CompanyId != company.Value || forEmployee.TimeZoneId != "Asia/Amman")
                throw new InvalidOperationException("Employee attendance time settings did not resolve within the current company scope.");

            var retry = await repository.InitializeAsync("Asia/Amman", marker + "_RETRY");
            if (retry.CompanyId != company.Value || retry.TimeZoneId != "Asia/Amman" || retry.CreationUser != marker)
                throw new InvalidOperationException("An idempotent attendance time-settings retry changed the existing configuration or its audit actor.");

            try
            {
                await repository.InitializeAsync("UTC", marker + "_CHANGE");
                throw new InvalidOperationException("Attendance time-settings initialization accepted an undated time-zone change.");
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("dated migration", StringComparison.Ordinal))
            {
                // Changing an established company zone requires a reviewed dated migration.
            }

            var persistedInsideTransaction = await db.Set<AttendanceTimeSettings>().AsNoTracking()
                .SingleAsync(setting => setting.CompanyId == company.Value);
            if (persistedInsideTransaction.TimeZoneId != "Asia/Amman" || persistedInsideTransaction.CreationUser != marker)
                throw new InvalidOperationException("The time-zone setting was not persisted within the caller's transaction.");

            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            if (await db.Set<AttendanceTimeSettings>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(setting => setting.CompanyId == company.Value))
                throw new InvalidOperationException("Rollback left an attendance time-settings fixture in DEV_TEMPLATE.");

            report.Add("Oracle OPS-05 attendance time-settings repository: the real repository initialized a scoped company setting under a ReadCommitted outer transaction, preserved an identical retry and actor, rejected an undated zone change, then rollback removed the row and absence was verified.");
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
