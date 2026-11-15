using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class OrganizationRepository
{
    public Task<HrDepartment> WriteDepartmentAsync(string code, DepartmentWrite value, string? expectedVersion, string user, CancellationToken ct = default)
    {
        code = OrganizationCatalogRules.Code(code); value = OrganizationCatalogRules.Normalize(value);
        user = OrganizationCatalogRules.Text(user, 100, "User");
        if (expectedVersion != null) expectedVersion = OrganizationCatalogRules.ExpectedVersion(expectedVersion);
        return Write(async () =>
        {
            var current = await DepartmentAsync(code, ct);
            CheckVersion(current?.Version, expectedVersion);
            if (current != null) await CheckBranch(current.BranchId, ct);
            if (current == null && await db.Set<HrDepartment>().IgnoreQueryFilters().AnyAsync(d => d.DepartmentCode.Trim().ToUpper() == code, ct))
                throw new InvalidOperationException("Department code is unavailable.");
            await CheckBranch(value.BranchId, ct);
            var managerEmployeeCode = value.ManagerEmployeeCodeSpecified ? value.ManagerEmployeeCode : current?.ManagerEmployeeCode;
            var departments = await db.Set<HrDepartment>().IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
            if (value.ParentDepartmentCode != null)
            {
                var parent = await DepartmentAsync(value.ParentDepartmentCode, ct);
                if (parent == null || !parent.IsActive || parent.BranchId.HasValue && parent.BranchId != value.BranchId)
                    throw new ArgumentException("Parent department is unavailable or belongs to a different branch.");
                var visible = await Allowed().Select(d => new { d.DepartmentCode, d.ParentDepartmentCode }).ToListAsync(ct);
                EmployeeHierarchyRules.Validate(code, value.ParentDepartmentCode, visible.ToDictionary(d => d.DepartmentCode, d => d.ParentDepartmentCode, StringComparer.OrdinalIgnoreCase));
            }
            if (managerEmployeeCode != null &&
                (current == null || !string.Equals(current.ManagerEmployeeCode, managerEmployeeCode, StringComparison.OrdinalIgnoreCase) || current.BranchId != value.BranchId))
                await CheckDepartmentManager(managerEmployeeCode, value.BranchId, ct);
            if (current != null && (current.BranchId != value.BranchId || current.IsActive && !value.IsActive))
            {
                if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.DepartmentCode == code, ct) ||
                    await db.Set<PayrollRunLine>().IgnoreQueryFilters().AnyAsync(l => l.DepartmentCode == code, ct) ||
                    await db.Set<HrPosition>().IgnoreQueryFilters().AnyAsync(p => p.DepartmentCode == code, ct) ||
                    departments.Any(d => d.ParentDepartmentCode == code) || await LegacyReferences(code, true, ct))
                    throw new InvalidOperationException("Referenced departments cannot be moved or deactivated.");
            }
            var target = current == null ? new HrDepartment { DepartmentCode = code, CreationUser = user, CreationDate = DateTime.UtcNow } : await TrackedDepartment(code, ct);
            target.NameLocal = value.NameLocal; target.NameEn = value.NameEn; target.BranchId = value.BranchId;
            target.ParentDepartmentCode = value.ParentDepartmentCode; target.ManagerEmployeeCode = managerEmployeeCode; target.IsActive = value.IsActive;
            if (current == null) db.Add(target); else { target.UpdateUser = user; target.UpdateDate = DateTime.UtcNow; }
            await Save(target, ct);
            return target;
        }, ct);
    }

    public Task<HrPosition> WritePositionAsync(string code, PositionWrite value, string? expectedVersion, string user, CancellationToken ct = default)
    {
        code = OrganizationCatalogRules.Code(code); value = OrganizationCatalogRules.Normalize(value);
        user = OrganizationCatalogRules.Text(user, 100, "User");
        if (expectedVersion != null) expectedVersion = OrganizationCatalogRules.ExpectedVersion(expectedVersion);
        return Write(async () =>
        {
            var current = await PositionAsync(code, ct);
            CheckVersion(current?.Version, expectedVersion);
            if (current != null)
            {
                var originalDepartment = await DepartmentAsync(current.DepartmentCode, ct) ?? throw new KeyNotFoundException("Department not found.");
                await CheckBranch(originalDepartment.BranchId, ct);
            }
            if (current == null && await db.Set<HrPosition>().IgnoreQueryFilters().AnyAsync(p => p.PositionCode.Trim().ToUpper() == code, ct))
                throw new InvalidOperationException("Position code is unavailable.");
            var department = await DepartmentAsync(value.DepartmentCode, ct);
            if (department == null || !department.IsActive) throw new ArgumentException("Department is unavailable or inactive.");
            await CheckBranch(department.BranchId, ct);
            if (value.ReportsToPositionCode != null)
            {
                var parent = await PositionAsync(value.ReportsToPositionCode, ct);
                var parentDepartment = parent == null ? null : await DepartmentAsync(parent.DepartmentCode, ct);
                if (parent == null || !parent.IsActive || parentDepartment == null || !parentDepartment.IsActive ||
                    parentDepartment.BranchId.HasValue && parentDepartment.BranchId != department.BranchId)
                    throw new ArgumentException("Reporting position is unavailable or belongs to a different branch.");
                var visibleDepartments = Allowed();
                var nodes = await db.Set<HrPosition>().AsNoTracking().Where(p => visibleDepartments.Any(d => d.DepartmentCode == p.DepartmentCode))
                    .Select(p => new { p.PositionCode, p.ReportsToPositionCode }).ToListAsync(ct);
                EmployeeHierarchyRules.Validate(code, value.ReportsToPositionCode, nodes.ToDictionary(p => p.PositionCode, p => p.ReportsToPositionCode, StringComparer.OrdinalIgnoreCase));
            }
            if (current != null && (current.DepartmentCode != value.DepartmentCode || current.IsActive && !value.IsActive))
            {
                if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.PositionCode == code, ct) ||
                    await db.Set<HrPosition>().IgnoreQueryFilters().AnyAsync(p => p.ReportsToPositionCode == code, ct) || await LegacyReferences(code, false, ct))
                    throw new InvalidOperationException("Referenced positions cannot be moved or deactivated.");
            }
            var target = current == null ? new HrPosition { PositionCode = code, CreationUser = user, CreationDate = DateTime.UtcNow } : await TrackedPosition(code, ct);
            target.DepartmentCode = value.DepartmentCode; target.TitleLocal = value.TitleLocal; target.TitleEn = value.TitleEn;
            target.ReportsToPositionCode = value.ReportsToPositionCode; target.Headcount = value.Headcount; target.IsActive = value.IsActive;
            if (current == null) db.Add(target); else { target.UpdateUser = user; target.UpdateDate = DateTime.UtcNow; }
            await Save(target, ct);
            return target;
        }, ct);
    }

    private static void CheckVersion(string? current, string? expected)
    {
        if (expected == null && current != null) throw new InvalidOperationException("Organization code already exists.");
        if (expected != null && current == null) throw new KeyNotFoundException("Organization record not found.");
        if (expected != null && !string.Equals(expected, current, StringComparison.OrdinalIgnoreCase)) throw new OrganizationVersionConflictException();
    }
    private async Task CheckBranch(long? branchId, CancellationToken ct)
    {
        if (!branchId.HasValue)
        {
            if (db.HrScopeEnabled && !db.HrScopeAllBranches) throw new InvalidOperationException("Shared catalog writes require all-branch scope.");
            return;
        }
        if (!await db.SysBranches.AnyAsync(b => b.Id == branchId && b.CompanyId == db.HrScopeCompanyId && b.IsActive, ct) ||
            db.HrScopeEnabled && !db.HrScopeAllBranches && !db.HrScopeBranchIds.Contains(branchId.Value))
            throw new ArgumentException("Branch is unavailable in the current company scope.");
    }

    private async Task CheckDepartmentManager(string managerEmployeeCode, long? departmentBranchId, CancellationToken ct)
    {
        if (db.HrScopeCompanyId <= 0) throw new InvalidOperationException("Trusted company scope is required.");
        var manager = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.EmployeeCode == managerEmployeeCode && e.IsActive && e.EmploymentStatus == "ACTIVE")
            .Select(e => new { e.EmployeeCode, e.BranchId })
            .SingleOrDefaultAsync(ct);
        if (manager?.BranchId is not long managerBranchId)
            throw new ArgumentException("Department manager must be an active employee assigned to a branch in the current company.");
        var managerBranch = await db.SysBranches.AsNoTracking().SingleOrDefaultAsync(b =>
            b.Id == managerBranchId && b.CompanyId == db.HrScopeCompanyId && b.IsActive, ct);
        if (managerBranch == null)
            throw new ArgumentException("Department manager must be an active employee in the current company.");
        if (departmentBranchId.HasValue && departmentBranchId.Value != managerBranchId)
            throw new ArgumentException("A branch-scoped department manager must belong to the same branch.");
    }
    private async Task<HrDepartment> TrackedDepartment(string code, CancellationToken ct)
    {
        var result = await db.Set<HrDepartment>().SingleAsync(d => d.DepartmentCode == code, ct);
        if (db.Database.IsRelational()) await db.Entry(result).ReloadAsync(ct);
        return result;
    }
    private async Task<HrPosition> TrackedPosition(string code, CancellationToken ct)
    {
        var result = await db.Set<HrPosition>().SingleAsync(p => p.PositionCode == code, ct);
        if (db.Database.IsRelational()) await db.Entry(result).ReloadAsync(ct);
        return result;
    }
    private async Task Save(object target, CancellationToken ct)
    {
        await new EmployeeStorageTextValidator(db).ValidateAsync(db.Entry(target), ct);
        await db.SaveChangesAsync(ct);
        if (db.Database.IsRelational()) await db.Entry(target).ReloadAsync(ct);
    }
    private async Task<bool> LegacyReferences(string code, bool department, CancellationToken ct)
    {
        // Existing recruitment and historical payroll tables have no full EF workflow.
        // Fixed SQL, no user-controlled identifiers; only existence is exposed.
        if (!db.Database.IsOracle()) return false;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = department
            ? "SELECT 1 FROM DUAL WHERE EXISTS (SELECT 1 FROM HR_JOB_REQUISITION WHERE DEPARTMENT_CODE=:code)"
            : "SELECT 1 FROM DUAL WHERE EXISTS (SELECT 1 FROM HR_JOB_REQUISITION WHERE POSITION_CODE=:code)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "code"; parameter.Value = code; command.Parameters.Add(parameter);
        ((Oracle.ManagedDataAccess.Client.OracleCommand)command).BindByName = true;
        return await command.ExecuteScalarAsync(ct) != null;
    }
    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.HrScopeCompanyId <= 0 || db.HrScopeEmployeeCode != null) throw new InvalidOperationException("Trusted company management scope is required.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Catalog writes require a clean unit of work.");
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Catalog writes require ReadCommitted.");
        await using var owned = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        try
        {
            if (db.Database.IsOracle())
            {
                // Same branch anchors as employee writes, ordered by company. Shared catalog
                // chains can cross branches: serialize their validation within this tenant.
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText = "SELECT \"Id\" FROM SYS_BRANCH ORDER BY COMPANY_ID,\"Id\" FOR UPDATE";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) { }
            }
            var result = await action();
            if (owned != null) await owned.CommitAsync(ct);
            return result;
        }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }
}
