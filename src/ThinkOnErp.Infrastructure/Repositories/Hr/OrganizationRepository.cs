using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
namespace ThinkOnErp.Infrastructure.Repositories.Hr;
public sealed partial class OrganizationRepository(OracleDbContext db) : IOrganizationRepository
{
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
