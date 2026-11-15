using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
internal static class HrEmployeeProfileVerification
{
    internal static UpdateEmployeeDto Profile(Employee e) => new(e.NameLocal, e.NameEn, e.NationalId, e.Nationality, e.PassportNumber, e.DateOfBirth, e.Gender, e.MaritalStatus,
        e.Email, e.Phone, e.HireDate, e.ProbationEndDate, e.TerminationDate, e.TerminationReason, e.EmploymentType, e.EmploymentStatus, e.DepartmentCode, e.PositionCode,
        e.BranchId, e.ManagerEmployeeCode, e.SscNumber, e.TaxExemptionCount, e.IsHighRiskRole, e.BankName, e.BankAccountNumber, e.BankIban, e.IsActive);
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue && e.IsActive && e.UserId == null && e.HireDate <= DateTime.UtcNow &&
                            !db.PayrollRuns.Any(r => (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID") && r.Lines.Any(l => l.EmployeeCode == e.EmployeeCode))
                          select new { e.EmployeeCode, BranchId = b.Id, CompanyId = b.CompanyId!.Value, e.TaxExemptionCount, e.IsActive, e.TerminationDate }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        if (await db.PayrollPeriods.AnyAsync(p => p.CompanyId == seed.CompanyId && p.Status == "CLOSED"))
            throw new InvalidOperationException("Profile fixture requires a company without original closed periods; original periods are never reopened.");
        var creationBranch = await db.SysBranches.AsNoTracking().Where(b => b.CompanyId == seed.CompanyId && b.IsActive &&
            !db.PayrollRuns.Any(r => r.BranchId == b.Id && (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID"))).FirstOrDefaultAsync();
        var prefix = "EP_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(); long dependentId = 0, periodId = 0, runId = 0;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var repository = new EmployeeRepository(db); var service = new EmployeeService(repository, new HrUnitOfWork(db));
            var employee = await repository.GetEmployeeByCodeAsync(seed.EmployeeCode) ?? throw new InvalidOperationException("Profile seed missing.");
            var request = Profile(employee) with { TaxExemptionCount = employee.TaxExemptionCount + 1 };
            await using (var cap = connection.CreateCommand())
            {
                cap.Transaction = (OracleTransaction)tx.GetDbTransaction();
                cap.CommandText = "SELECT CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE' AND COLUMN_NAME='NAME_EN'";
                var max = Convert.ToInt32(await cap.ExecuteScalarAsync());
                if (max < 400)
                {
                    var previewStorageRejected = false;
                    try { await repository.ValidateStorageAsync(new Employee { EmployeeCode = seed.EmployeeCode, NameEn = new string('X', max + 1) }); }
                    catch (ArgumentException ex) when (ex.Message.Contains("storage limit")) { previewStorageRejected = true; }
                    if (!previewStorageRejected) throw new InvalidOperationException("Detached employee preview bypassed tenant character capacity.");
                    var storageRejected = false;
                    try { await service.UpdateEmployeeAsync(seed.EmployeeCode, request with { NameEn = new string('X', max + 1) }, "HR_VERIFY"); }
                    catch (ArgumentException ex) when (ex.Message.Contains("storage limit")) { storageRejected = true; }
                    if (!storageRejected) throw new InvalidOperationException("Tenant character limit was not validated before SQL persistence.");
                    db.ChangeTracker.Clear();
                    report.Add("Oracle employee input: tenant character capacity narrower than EF is rejected before DML; no schema widths changed.");
                }
            }
            if (!await repository.ExistsByNationalIdAsync(employee.NationalId) || await repository.ExistsByNationalIdAsync(employee.NationalId, seed.EmployeeCode))
                throw new InvalidOperationException("Employee national-ID SQL/exclusion validation failed.");

            await service.UpdateEmployeeAsync(seed.EmployeeCode, request, "HR_VERIFY");
            var dep = await service.AddDependentAsync(seed.EmployeeCode, new(prefix, prefix, "CHILD", new DateTime(2010, 1, 1), "M", null, true, false), "HR_VERIFY");
            dependentId = dep.Id;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE_DEPENDENT SET NAME_EN={prefix + "_SQL"} WHERE ID={dependentId}");
            await service.UpdateEmployeeAsync(seed.EmployeeCode, request with { TaxExemptionCount = request.TaxExemptionCount + 1 }, "HR_VERIFY");
            if ((await db.EmployeeDependents.AsNoTracking().SingleAsync(d => d.Id == dependentId)).NameEn != prefix + "_SQL")
                throw new InvalidOperationException("Updating employee rewrote a stale dependent graph.");
            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = prefix, NameAr = "Profile fixture", NameEn = "Profile fixture", FiscalYear = DateTime.UtcNow.Year,
                Month = DateTime.UtcNow.Month, StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date, PayDate = DateTime.UtcNow.Date, Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync(); periodId = period.Id;
            var create = new CreateEmployeeDto(prefix + "_NEW", prefix, prefix, prefix, "Fixture", null, new DateTime(1990, 1, 1), "M", "SINGLE", null, null,
                DateTime.UtcNow.Date, null, "FULL_TIME", "ACTIVE", null, null, creationBranch?.Id ?? seed.BranchId, null, null, 0, false, null, null, null);
            var rejected = 0;
            async Task Closed(Func<Task> action)
            {
                try { await action(); } catch (InvalidOperationException ex) when (ex.Message.Contains("closed payroll period")) { rejected++; return; }
                throw new InvalidOperationException("Profile mutation bypassed the closed-period guard.");
            }
            await Closed(() => service.CreateEmployeeAsync(create, "HR_VERIFY"));
            await Closed(() => service.UpdateEmployeeAsync(seed.EmployeeCode, request with { IsActive = false }, "HR_VERIFY"));
            await Closed(() => service.DeleteEmployeeAsync(seed.EmployeeCode, "HR_VERIFY"));
            await Closed(() => service.AddDependentAsync(seed.EmployeeCode, new(prefix, prefix, "CHILD", new DateTime(2010, 1, 1), "M", null, true, false), "HR_VERIFY"));
            await Closed(() => service.DeleteDependentAsync(dependentId, "HR_VERIFY"));
            if (rejected != 5 || !await db.EmployeeDependents.AnyAsync(d => d.Id == dependentId) || !(await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == seed.EmployeeCode)).IsActive)
                throw new InvalidOperationException("Rejected profile mutations left partial effects.");
            period.Status = "OPEN"; await db.SaveChangesAsync();
            var run = new PayrollRun { PayPeriod = prefix + "_ORPHAN", BranchId = seed.BranchId, Status = "APPROVED", CreationUser = "HR_VERIFY" };
            run.Lines.Add(new PayrollRunLine { EmployeeCode = seed.EmployeeCode, BranchId = seed.BranchId, CreationUser = "HR_VERIFY" }); db.Add(run); await db.SaveChangesAsync(); runId = run.Id;
            var approvedBlocked = false;
            try { await service.UpdateEmployeeAsync(seed.EmployeeCode, request, "HR_VERIFY"); }
            catch (DbUpdateException ex) when (ex.InnerException is OracleException)
        {
            var oracle = (OracleException)ex.InnerException!;
            report.Add("Employee profile fixture DML: ORA-" + oracle.Number.ToString("D5") + "; identifiers=" +
                string.Join("/", System.Text.RegularExpressions.Regex.Matches(oracle.Message, "\"[A-Z][A-Z0-9_]*\"").Select(m => m.Value).Take(4)));
            throw;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("approved payroll")) { approvedBlocked = true; }
            if (!approvedBlocked) throw new InvalidOperationException("Legacy approved run without a period record failed to protect profile history.");
            var orphanCreateBlocked = false;
            try { await service.CreateEmployeeAsync(create with { BranchId = seed.BranchId }, "HR_VERIFY"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Legacy approved payroll")) { orphanCreateBlocked = true; }
            if (!orphanCreateBlocked || await db.Employees.AnyAsync(e => e.EmployeeCode == create.EmployeeCode)) throw new InvalidOperationException("New employee bypassed legacy approved branch payroll.");
            run.Status = "DRAFT"; await db.SaveChangesAsync();
            if (creationBranch != null)
            {
                await service.CreateEmployeeAsync(create, "HR_VERIFY");
                if (!await db.Employees.AnyAsync(e => e.EmployeeCode == create.EmployeeCode)) throw new InvalidOperationException("Allowed direct employee creation failed.");
                var duplicateRejected = false;
                try { await service.CreateEmployeeAsync(create with { EmployeeCode = " " + create.EmployeeCode.ToLowerInvariant() + " " }, "HR_VERIFY"); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists")) { duplicateRejected = true; }
                if (!duplicateRejected || await db.Employees.CountAsync(e => e.EmployeeCode == create.EmployeeCode) != 1)
                    throw new InvalidOperationException("Canonical duplicate employee code was accepted.");
                report.Add("Oracle normalized employee-code duplicate rejected; company identity anchor locking and national-ID SQL accepted.");
                report.Add("Oracle allowed direct creation passed on an existing active branch without approved payroll; no original branch or payroll state changed.");
            }
            else report.Add("Oracle allowed direct creation SQL was not exercised: all current-company active branches have original approved payroll. Unit callback coverage remains separate; original payroll was not changed.");
            await service.DeleteDependentAsync(dependentId, "HR_VERIFY");
            if (await db.EmployeeDependents.AnyAsync(d => d.Id == dependentId)) throw new InvalidOperationException("Allowed dependent delete failed.");
            report.Add("Oracle employee profile: allowed update/add/delete under shared payroll locks, stale child graph preserved, all four profile mutations and direct creation rejected by CLOSED without partial effects, and approved legacy run without period mapping blocks profile edits and branch creation; direct creation guards passed (allowed SQL case reported separately).");
        }
        catch (DbUpdateException ex) when (ex.InnerException is OracleException)
        {
            var oracle = (OracleException)ex.InnerException!;
            report.Add("Employee profile fixture DML: ORA-" + oracle.Number.ToString("D5") + "; identifiers=" +
                string.Join("/", System.Text.RegularExpressions.Regex.Matches(oracle.Message, "\"[A-Z][A-Z0-9_]*\"").Select(m => m.Value).Take(4)));
            throw;
        }
        catch (InvalidOperationException ex) { report.Add("Employee profile fixture: " + ex.Message.Replace(seed.EmployeeCode, "<employee>")); throw; }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
        var restored = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == seed.EmployeeCode);
        if (restored.TaxExemptionCount != seed.TaxExemptionCount || restored.IsActive != seed.IsActive || restored.TerminationDate != seed.TerminationDate ||
            await db.Employees.AnyAsync(e => e.EmployeeCode == prefix + "_NEW") ||
            await db.EmployeeDependents.AnyAsync(d => d.Id == dependentId) || await db.PayrollPeriods.AnyAsync(p => p.Id == periodId) || await db.PayrollRuns.AnyAsync(r => r.Id == runId))
            throw new InvalidOperationException("Employee profile fixture rollback failed.");
        report.Add("Employee profile rollback verified; original employee/dependent/payroll data retained. Own commit lifecycle, concurrent Oracle sessions and profile HTTP not tested.");
    }
}
