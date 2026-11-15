using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
namespace ThinkOnErp.Infrastructure.Repositories.Hr;
internal static class EmployeeOrganizationValidator
{
    public static async Task ValidateAsync(OracleDbContext db, EntityEntry<Employee> entry, CancellationToken ct)
    {
        var changed = entry.State == EntityState.Added || new[] { nameof(Employee.DepartmentCode), nameof(Employee.PositionCode), nameof(Employee.ManagerEmployeeCode), nameof(Employee.BranchId) }
            .Any(name => !Equals(entry.Property(name).OriginalValue, entry.Property(name).CurrentValue));
        if (!changed) return; // Unchanged legacy links are reviewed separately, not rewritten by contact edits.
        if (db.HrScopeCompanyId <= 0) throw new InvalidOperationException("Trusted company scope is required for employee organization changes.");
        var e = entry.Entity;
        var departments = db.Set<HrDepartment>().AsNoTracking().Where(d => !d.BranchId.HasValue || db.SysBranches.Any(b => b.Id == d.BranchId && b.CompanyId == db.HrScopeCompanyId));
        if (e.DepartmentCode != null)
        {
            var department = await departments.SingleOrDefaultAsync(d => d.DepartmentCode == e.DepartmentCode && d.IsActive, ct);
            if (department == null || department.BranchId.HasValue && department.BranchId != e.BranchId)
                throw new ArgumentException("Department is unavailable or does not belong to the employee branch.");
        }
        if (e.PositionCode != null && (e.DepartmentCode == null || !await db.Set<HrPosition>().AsNoTracking().AnyAsync(p => p.PositionCode == e.PositionCode && p.DepartmentCode == e.DepartmentCode && p.IsActive, ct)))
            throw new ArgumentException("Position is unavailable or does not belong to the selected department.");
        if (e.ManagerEmployeeCode == null) return;
        if (!await db.Employees.AsNoTracking().AnyAsync(m => m.EmployeeCode == e.ManagerEmployeeCode && m.IsActive && m.EmploymentStatus == "ACTIVE", ct))
            throw new ArgumentException("Manager is unavailable in the current employee/branch scope.");
        var nodes = await db.Employees.IgnoreQueryFilters().AsNoTracking().Where(m => !m.BranchId.HasValue ||
            db.SysBranches.Any(b => b.Id == m.BranchId && b.CompanyId == db.HrScopeCompanyId))
            .Select(m => new { m.EmployeeCode, m.ManagerEmployeeCode }).ToListAsync(ct);
        var managers = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
            if (!managers.TryAdd(node.EmployeeCode, node.ManagerEmployeeCode)) throw new ArgumentException("Ambiguous employee codes in reporting chain; review legacy data.");
        EmployeeHierarchyRules.Validate(e.EmployeeCode, e.ManagerEmployeeCode, managers);
    }
}
