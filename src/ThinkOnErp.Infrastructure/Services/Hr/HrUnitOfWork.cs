using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class HrUnitOfWork(OracleDbContext db) : IHrUnitOfWork
{
    public async Task<T> ExecuteEmployeeAccountAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.HrScopeCompanyId <= 0 || db.HrScopeEmployeeCode != null)
            throw new InvalidOperationException("Trusted company management scope is required for account changes.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Account changes require a clean unit of work.");
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Account changes require ReadCommitted.");
        if (db.Database.IsRelational() && !db.Database.IsOracle()) throw new InvalidOperationException("Account locking requires Oracle.");
        await using var owned = db.Database.IsRelational() && db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var before = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken)
                ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
            var companyId = before.BranchId.HasValue
                ? await db.SysBranches.Where(b => b.Id == before.BranchId && b.IsActive).Select(b => b.CompanyId).SingleOrDefaultAsync(cancellationToken)
                : db.HrScopeCompanyId;
            if (companyId != db.HrScopeCompanyId) throw new InvalidOperationException("Employee company is outside the account management scope.");
            if (db.Database.IsOracle())
            {
                await LockCompanyIdentityAsync(companyId.Value, cancellationToken);
                // Coordinate with calculation/approval as well as profile/import writers.
                // CLOSED periods are locked but do not prevent a security unlink.
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                    command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID=:companyId ORDER BY ID FOR UPDATE";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "companyId"; parameter.Value = companyId.Value; command.Parameters.Add(parameter);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken)) { }
                }
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                    command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:code FOR UPDATE";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "code"; parameter.Value = employeeCode; command.Parameters.Add(parameter);
                    if (await command.ExecuteScalarAsync(cancellationToken) == null) throw new KeyNotFoundException("Employee not found.");
                }
            }
            var current = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
            if (current == null || current.BranchId != before.BranchId)
                throw new InvalidOperationException("Employee branch or visibility changed while acquiring account locks. Retry the operation.");
            foreach (var entry in db.ChangeTracker.Entries<ThinkOnErp.Domain.Entities.Hr.Employee>().Where(e => e.Entity.EmployeeCode == employeeCode).ToArray())
                await entry.ReloadAsync(cancellationToken);
            var result = await action();
            if (owned != null) await owned.CommitAsync(cancellationToken);
            return result;
        }
        catch { if (owned != null) { try { await owned.RollbackAsync(CancellationToken.None); } finally { db.ChangeTracker.Clear(); } } throw; }
    }

    public Task<EmployeeImportResultDto> ExecuteEmployeeImportAsync(IReadOnlyList<EmployeeImportRowDto> rows,
        Func<Task<EmployeeImportResultDto>> action, CancellationToken cancellationToken = default)
        => ExecuteEmployeeInputsAsync(rows, action, result => result.Errors.Count > 0, true, cancellationToken);

    public Task<T> ExecuteNewEmployeeAsync<T>(EmployeeImportRowDto row, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => ExecuteEmployeeInputsAsync(new[] { row }, action, _ => false, false, cancellationToken);

    private async Task<T> ExecuteEmployeeInputsAsync<T>(IReadOnlyList<EmployeeImportRowDto> rows, Func<Task<T>> action,
        Func<T, bool> hasErrors, bool requireOwnedTransaction, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction != null && (requireOwnedTransaction || db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted))
            throw new InvalidOperationException("Employee import owns its transaction; new employee creation requires ReadCommitted.");
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var scopes = new List<(string Code, long CompanyId, long? BranchId, DateTime From, bool Existing)>();
            foreach (var row in rows)
            {
                var employee = await db.Employees.AsNoTracking().Include(e => e.SalaryStructures)
                    .SingleOrDefaultAsync(e => e.EmployeeCode == row.EmployeeCode, cancellationToken);
                var from = row.HireDate?.Date ?? DateTime.UtcNow.Date;
                if (row.SalaryEffectiveFrom.HasValue && row.SalaryEffectiveFrom.Value.Date < from)
                    from = row.SalaryEffectiveFrom.Value.Date;
                if (employee != null)
                {
                    if (employee.HireDate.Date < from) from = employee.HireDate.Date;
                    foreach (var salary in employee.SalaryStructures)
                        if (salary.EffectiveFrom.Date < from) from = salary.EffectiveFrom.Date;
                }
                var branchIds = employee == null ? new[] { row.BranchId } : new[] { employee.BranchId, row.BranchId };
                foreach (var branchId in branchIds.Distinct())
                {
                    if (db.HrScopeEnabled && !db.HrScopeAllBranches && (!branchId.HasValue || !db.HrScopeBranchIds.Contains(branchId.Value)))
                        throw new UnauthorizedAccessException("Employee input branch is outside the permitted scope.");
                    if (branchId.HasValue && !await db.SysBranches.AnyAsync(b => b.Id == branchId && b.IsActive, cancellationToken))
                        throw new UnauthorizedAccessException("Employee input branch is unavailable.");
                    var companyId = branchId.HasValue
                        ? await db.SysBranches.Where(b => b.Id == branchId.Value).Select(b => b.CompanyId).SingleOrDefaultAsync(cancellationToken)
                        : db.HrScopeCompanyId > 0 ? db.HrScopeCompanyId : (long?)null;
                    if (!companyId.HasValue || (db.HrScopeEnabled && companyId != db.HrScopeCompanyId))
                        throw new InvalidOperationException($"Company could not be resolved within the authorized scope for employee input '{row.EmployeeCode}'.");
                    scopes.Add((row.EmployeeCode, companyId.Value, branchId, from, employee != null));
                }
            }

            async Task Lock(string sql, params (string Name, object Value)[] parameters)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText = sql;
                foreach (var item in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = item.Name;
                    parameter.Value = item.Value;
                    command.Parameters.Add(parameter);
                }
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) { }
            }

            foreach (var companyId in scopes.Select(s => s.CompanyId).Distinct().OrderBy(id => id))
                await LockCompanyIdentityAsync(companyId, cancellationToken);

            // Lock the union of affected periods once, before locking individual employees.
            foreach (var company in scopes.GroupBy(s => s.CompanyId).OrderBy(g => g.Key))
                await Lock("SELECT ID FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID = :companyId AND END_DATE >= :fromDate ORDER BY ID FOR UPDATE",
                    ("companyId", company.Key), ("fromDate", company.Min(s => s.From)));
            foreach (var code in scopes.Where(s => s.Existing).Select(s => s.Code).Distinct().OrderBy(c => c, StringComparer.Ordinal))
                await Lock("SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE = :employeeCode FOR UPDATE", ("employeeCode", code));

            foreach (var scope in scopes)
            {
                var periods = db.PayrollPeriods.Where(p => p.CompanyId == scope.CompanyId && p.EndDate >= scope.From);
                if (await periods.AnyAsync(p => p.Status == "CLOSED", cancellationToken))
                    throw new InvalidOperationException($"Employee '{scope.Code}' input overlaps a closed payroll period.");
                var codes = periods.Select(p => p.PeriodCode);
                if (await db.PayrollRuns.AnyAsync(r => codes.Contains(r.PayPeriod) &&
                    (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID") &&
                    (r.Lines.Any(l => l.EmployeeCode == scope.Code) || (scope.BranchId.HasValue && r.BranchId == scope.BranchId)), cancellationToken))
                    throw new InvalidOperationException($"Employee '{scope.Code}' input would change inputs of approved payroll. Resolve the dependent payroll first.");
            }

            // Excel must protect orphan legacy approvals just as direct creation does.
            foreach (var scope in scopes)
                if (await db.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => (scope.BranchId.HasValue && r.BranchId == scope.BranchId ||
                    scope.Existing && r.Lines.Any(l => l.EmployeeCode == scope.Code)) &&
                    (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID") &&
                    !db.PayrollPeriods.IgnoreQueryFilters().Any(p => p.CompanyId == scope.CompanyId && p.PeriodCode == r.PayPeriod), cancellationToken))
                    throw new InvalidOperationException("Legacy approved payroll has no period mapping; review it before changing employee inputs.");

            var result = await action();
            if (hasErrors(result))
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return result;
            }
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            if (transaction != null) db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task LockCompanyIdentityAsync(long companyId, CancellationToken ct)
    {
        if (companyId <= 0) throw new InvalidOperationException("A company is required for employee identity locking.");
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT \"Id\" FROM SYS_BRANCH WHERE \"Id\"=(SELECT MIN(\"Id\") FROM SYS_BRANCH WHERE COMPANY_ID=:companyId) FOR UPDATE";
        var parameter = command.CreateParameter(); parameter.ParameterName = "companyId"; parameter.Value = companyId; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct) == null) throw new InvalidOperationException("Company branch anchor is missing; cannot validate employee identities safely.");
    }

    public async Task<T> ExecuteEmployeeProfileAsync<T>(string employeeCode, DateTime earliestDate, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Employee profile changes require ReadCommitted.");
        await using var owned = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var before = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
            var companyId = before.BranchId.HasValue ? await db.SysBranches.Where(b => b.Id == before.BranchId).Select(b => b.CompanyId).SingleAsync(cancellationToken) : (long?)db.HrScopeCompanyId;
            await LockCompanyIdentityAsync(companyId ?? 0, cancellationToken);
            await HrEmployeePayrollLock.AcquireAsync(db, employeeCode,
                new[] { new ThinkOnErp.Domain.Entities.Hr.HrDateRange(earliestDate.Date, DateTime.MaxValue.Date) }, cancellationToken);
            var current = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == employeeCode, cancellationToken);
            if (current.HireDate.Date < earliestDate.Date) throw new InvalidOperationException("Employee employment history changed while acquiring locks. Reload and retry.");
            // Profiles are undated inputs. Even legacy approved runs without a period
            // record must prevent rewriting their employee/dependent history.
            var runIds = db.PayrollRunLines.IgnoreQueryFilters().Where(l => l.EmployeeCode == employeeCode).Select(l => l.PayrollRunId);
            if (await db.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => runIds.Contains(r.Id) &&
                (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID"), cancellationToken))
                throw new InvalidOperationException("Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.");
            var result = await action(); if (owned != null) await owned.CommitAsync(cancellationToken); return result;
        }
        catch { if (owned != null) { await owned.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); } throw; }
    }

    public async Task<T> ExecuteForEmployeeDatesAsync<T>(string employeeCode, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (endDate.Date < startDate.Date) throw new ArgumentException("End date cannot precede start date.");
        if (db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Payroll input locking must start its own transaction.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await HrEmployeePayrollLock.AcquireAsync(db, employeeCode,
                new[] { new ThinkOnErp.Domain.Entities.Hr.HrDateRange(startDate.Date, endDate.Date) }, cancellationToken);

            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<T> ExecuteForCompanyPayrollDatesAsync<T>(long companyId, DateTime startDate, DateTime endDate,
        Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) throw new ArgumentOutOfRangeException(nameof(companyId));
        if (endDate.Date < startDate.Date) throw new ArgumentException("End date cannot precede start date.");
        if (db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Payroll policy locking must start its own transaction.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await LockCompanyIdentityAsync(companyId, cancellationToken);
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID=:companyId AND END_DATE>=:startDate AND START_DATE<=:endDate ORDER BY ID FOR UPDATE";
                var companyParameter = command.CreateParameter(); companyParameter.ParameterName = "companyId"; companyParameter.Value = companyId; command.Parameters.Add(companyParameter);
                var startParameter = command.CreateParameter(); startParameter.ParameterName = "startDate"; startParameter.Value = startDate.Date; command.Parameters.Add(startParameter);
                var endParameter = command.CreateParameter(); endParameter.ParameterName = "endDate"; endParameter.Value = endDate.Date; command.Parameters.Add(endParameter);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) { }
            }

            var periodCodes = await db.PayrollPeriods.IgnoreQueryFilters()
                .Where(p => p.CompanyId == companyId && p.EndDate >= startDate.Date && p.StartDate <= endDate.Date)
                .Select(p => p.PeriodCode).ToArrayAsync(cancellationToken);
            var companyBranches = await db.SysBranches.IgnoreQueryFilters()
                .Where(b => b.CompanyId == companyId).Select(b => b.Id).ToArrayAsync(cancellationToken);
            if (periodCodes.Length > 0 && companyBranches.Length > 0 &&
                await db.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => periodCodes.Contains(r.PayPeriod) &&
                    ((r.BranchId.HasValue && companyBranches.Contains(r.BranchId.Value)) ||
                     r.Lines.Any(l => l.BranchId.HasValue && companyBranches.Contains(l.BranchId.Value))) &&
                    (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID"), cancellationToken))
                throw new InvalidOperationException("Payroll policy changes affect approved payroll. Reverse its dependent payroll before changing policies.");

            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<T> ExecutePayrollFinancialAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Payroll posting and reversal require a clean unit of work.");
        return ExecuteForPayrollPeriodAsync(companyId, periodCode, () =>
        {
            // Discard cached reads only after the lock: a waiter must see the preceding
            // holder's committed run/journal state, not tracked pre-lock entities.
            db.ChangeTracker.Clear();
            return action();
        }, cancellationToken);
    }

    public async Task<T> ExecuteForPayrollPeriodAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        // ReadCommitted ensures a waiter reads the preceding holder's committed changes after
        // acquiring the period lock; an Oracle serializable snapshot would retain older inputs.
        if (db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Payroll input locking must start its own transaction.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID = :companyId AND PERIOD_CODE = :periodCode ORDER BY ID FOR UPDATE";
            var companyParameter = command.CreateParameter();
            companyParameter.ParameterName = "companyId";
            companyParameter.Value = companyId;
            command.Parameters.Add(companyParameter);
            var periodParameter = command.CreateParameter();
            periodParameter.ParameterName = "periodCode";
            periodParameter.Value = periodCode;
            command.Parameters.Add(periodParameter);
            var found = false;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) found = true;
            if (!found) throw new InvalidOperationException("The payroll period must exist before calculating payroll or changing adjustment approval.");
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<T> ExecuteForPayrollPeriodsAsync<T>(long companyId, string requiredPeriodCode, IReadOnlyCollection<string> affectedPeriodCodes,
        Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) throw new ArgumentOutOfRangeException(nameof(companyId));
        requiredPeriodCode = requiredPeriodCode?.Trim() ?? string.Empty;
        var codes = affectedPeriodCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim())
            .Append(requiredPeriodCode).Where(code => code.Length is > 0 and <= 20)
            .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        if (requiredPeriodCode.Length is < 1 or > 20 || codes.Length == 0)
            throw new ArgumentException("A required payroll period and valid affected periods are required.");
        if (db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Payroll period locking must start its own transaction.");
        if (db.Database.IsRelational() && !db.Database.IsOracle())
            throw new InvalidOperationException("Payroll period locking requires Oracle.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            var parameters = codes.Select((_, index) => $":period{index}").ToArray();
            command.CommandText = $"SELECT PERIOD_CODE FROM HR_PAYROLL_PERIOD WHERE COMPANY_ID=:companyId AND PERIOD_CODE IN ({string.Join(",", parameters)}) ORDER BY PERIOD_CODE FOR UPDATE";
            var company = command.CreateParameter(); company.ParameterName = "companyId"; company.Value = companyId; command.Parameters.Add(company);
            for (var index = 0; index < codes.Length; index++)
            {
                var period = command.CreateParameter(); period.ParameterName = $"period{index}"; period.Value = codes[index]; command.Parameters.Add(period);
            }

            var locked = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) locked.Add(reader.GetString(0));
            if (!locked.Contains(requiredPeriodCode))
                throw new InvalidOperationException("The source payroll period must exist before accepting its Accounting posting receipt.");

            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction != null) return await action();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try { var result = await action(); await transaction.CommitAsync(cancellationToken); return result; }
        catch { await transaction.RollbackAsync(cancellationToken); db.ChangeTracker.Clear(); throw; }
    }
}
