using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class RoleScreenPermissionRepository : IRoleScreenPermissionRepository
{
    private readonly OracleDbContext _context;
    public RoleScreenPermissionRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysRoleScreenPermission>> GetByBranchAndRoleAsync(long branchId, long roleId)
    {
        return await _context.Set<SysRoleScreenPermission>()
            .Where(p => p.BranchId == branchId && p.RoleId == roleId)
            .OrderBy(p => p.ScreenId).ThenBy(p => p.FeatureId)
            .ToListAsync();
    }

    public async Task BulkSetAsync(long branchId, long roleId, List<SysRoleScreenPermission> permissions)
    {
        var existing = await _context.Set<SysRoleScreenPermission>()
            .Where(p => p.BranchId == branchId && p.RoleId == roleId)
            .ToListAsync();
        _context.Set<SysRoleScreenPermission>().RemoveRange(existing);

        if (permissions.Count > 0)
        {
            var nextId = await GetNextIdAsync();
            foreach (var permission in permissions.Where(p => p.Id <= 0))
            {
                permission.Id = nextId++;
            }

            _context.Set<SysRoleScreenPermission>().AddRange(permissions);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<long> GetNextIdAsync()
    {
        var currentMax = await _context.Set<SysRoleScreenPermission>()
            .Select(p => (long?)p.Id)
            .MaxAsync() ?? 0;

        return currentMax + 1;
    }

    public async Task DeleteAsync(long branchId, long roleId, long screenId, long featureId)
    {
        var entry = await _context.Set<SysRoleScreenPermission>()
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.RoleId == roleId
                && p.ScreenId == screenId && p.FeatureId == featureId);
        if (entry != null)
        {
            _context.Set<SysRoleScreenPermission>().Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}
