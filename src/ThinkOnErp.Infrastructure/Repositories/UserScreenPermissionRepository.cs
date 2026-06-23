using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class UserScreenPermissionRepository : IUserScreenPermissionRepository
{
    private readonly OracleDbContext _context;
    public UserScreenPermissionRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysUserScreenPermission>> GetByBranchAndUserAsync(long branchId, long userId)
    {
        return await _context.Set<SysUserScreenPermission>()
            .Where(p => p.BranchId == branchId && p.UserId == userId)
            .OrderBy(p => p.ScreenId).ThenBy(p => p.FeatureId)
            .ToListAsync();
    }

    public async Task BulkSetAsync(long branchId, long userId, List<SysUserScreenPermission> permissions)
    {
        var existing = await _context.Set<SysUserScreenPermission>()
            .Where(p => p.BranchId == branchId && p.UserId == userId)
            .ToListAsync();
        _context.Set<SysUserScreenPermission>().RemoveRange(existing);

        if (permissions.Count > 0)
        {
            var nextId = await GetNextIdAsync();
            foreach (var permission in permissions.Where(p => p.Id <= 0))
            {
                permission.Id = nextId++;
            }

            _context.Set<SysUserScreenPermission>().AddRange(permissions);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<long> GetNextIdAsync()
    {
        var currentMax = await _context.Set<SysUserScreenPermission>()
            .Select(p => (long?)p.Id)
            .MaxAsync() ?? 0;

        return currentMax + 1;
    }

    public async Task DeleteAsync(long branchId, long userId, long screenId, long featureId)
    {
        var entry = await _context.Set<SysUserScreenPermission>()
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.UserId == userId
                && p.ScreenId == screenId && p.FeatureId == featureId);
        if (entry != null)
        {
            _context.Set<SysUserScreenPermission>().Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}
