using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
namespace ThinkOnErp.Infrastructure.Repositories.Hr;
public sealed partial class OrganizationRepository(OracleDbContext db) : IOrganizationRepository
{
    public async Task<OrganizationReferenceIntegritySummary> VerifyReferenceIntegrityAsync(CancellationToken ct = default)
    {
        var companyId = db.HrScopeCompanyId;
        if (companyId <= 0) throw new InvalidOperationException("Trusted company scope is required.");

        var companyBranches = db.SysBranches.AsNoTracking().Where(b => b.CompanyId == companyId).Select(b => b.Id);
        var departments = db.Set<HrDepartment>().IgnoreQueryFilters().AsNoTracking()
            .Where(d => !d.BranchId.HasValue || companyBranches.Contains(d.BranchId.Value));
        var positions = db.Set<HrPosition>().IgnoreQueryFilters().AsNoTracking()
            .Where(p => departments.Any(d => d.DepartmentCode == p.DepartmentCode));
        var employees = db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => !e.BranchId.HasValue || companyBranches.Contains(e.BranchId.Value));

        var employeeDepartment = await employees.LongCountAsync(e => e.DepartmentCode != null &&
            !departments.Any(d => d.DepartmentCode == e.DepartmentCode && d.IsActive), ct);
        var employeePosition = await employees.LongCountAsync(e => e.PositionCode != null &&
            (e.DepartmentCode == null || !positions.Any(p => p.PositionCode == e.PositionCode &&
                p.DepartmentCode == e.DepartmentCode && p.IsActive)), ct);
        // A terminated manager can remain as a valid historical link. Count only a missing code.
        var employeeManager = await employees.LongCountAsync(e => e.ManagerEmployeeCode != null &&
            !employees.Any(m => m.EmployeeCode == e.ManagerEmployeeCode), ct);
        var employeeBranch = await db.Employees.IgnoreQueryFilters().AsNoTracking().LongCountAsync(e => e.BranchId.HasValue &&
            !db.SysBranches.Any(b => b.Id == e.BranchId.Value && b.CompanyId == companyId), ct);
        var departmentParent = await departments.LongCountAsync(d => d.ParentDepartmentCode != null &&
            !departments.Any(parent => parent.DepartmentCode == d.ParentDepartmentCode && parent.IsActive &&
                (!parent.BranchId.HasValue || parent.BranchId == d.BranchId)), ct);
        var departmentManager = await departments.LongCountAsync(d => d.ManagerEmployeeCode != null &&
            !employees.Any(e => e.EmployeeCode == d.ManagerEmployeeCode), ct);
        var departmentBranch = await db.Set<HrDepartment>().IgnoreQueryFilters().AsNoTracking().LongCountAsync(d => d.BranchId.HasValue &&
            !db.SysBranches.Any(b => b.Id == d.BranchId.Value && b.CompanyId == companyId), ct);
        var positionDepartment = await positions.LongCountAsync(p =>
            !departments.Any(d => d.DepartmentCode == p.DepartmentCode && d.IsActive), ct);
        var positionReportsTo = await positions.LongCountAsync(p => p.ReportsToPositionCode != null &&
            !positions.Any(parent => parent.PositionCode == p.ReportsToPositionCode && parent.IsActive &&
                parent.DepartmentCode == p.DepartmentCode), ct);

        return new OrganizationReferenceIntegritySummary(
            employeeDepartment, employeePosition, employeeManager, employeeBranch,
            departmentParent, departmentManager, departmentBranch, positionDepartment, positionReportsTo, DateTime.UtcNow);
    }

    private IQueryable<HrDepartment> Allowed()
    {
        if (db.HrScopeCompanyId <= 0) throw new InvalidOperationException("Trusted company scope is required.");
        return db.Set<HrDepartment>().AsNoTracking().Where(d => !d.BranchId.HasValue || db.SysBranches.Any(b => b.Id == d.BranchId && b.CompanyId == db.HrScopeCompanyId));
    }
    public async Task<IReadOnlyList<HrDepartment>> DepartmentsAsync(CancellationToken ct = default)
        => await Allowed().Where(d => d.IsActive).OrderBy(d => d.DepartmentCode).ToListAsync(ct);
    public Task<HrDepartment?> DepartmentAsync(string code, CancellationToken ct = default)
        => Allowed().SingleOrDefaultAsync(d => d.DepartmentCode == code, ct);
    public Task<HrPosition?> PositionAsync(string code, CancellationToken ct = default)
    {
        var departments = Allowed();
        return db.Set<HrPosition>().AsNoTracking().SingleOrDefaultAsync(p => p.PositionCode == code && departments.Any(d => d.DepartmentCode == p.DepartmentCode), ct);
    }
    public async Task<IReadOnlyList<HrPosition>> PositionsAsync(string? departmentCode, CancellationToken ct = default)
    {
        var departments = Allowed().Where(d => d.IsActive);
        return await db.Set<HrPosition>().AsNoTracking().Where(p => p.IsActive && (string.IsNullOrEmpty(departmentCode) || p.DepartmentCode == departmentCode) &&
            departments.Any(d => d.DepartmentCode == p.DepartmentCode)).OrderBy(p => p.PositionCode).ToListAsync(ct);
    }
}
