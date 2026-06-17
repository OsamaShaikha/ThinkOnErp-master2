using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class BranchScreenRepository : IBranchScreenRepository
{
    private readonly OracleDbContext _context;
    public BranchScreenRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysScreen>> GetAvailableScreensAsync(long branchId)
    {
        var assignedSystemIds = await _context.Set<SysBranchSystem>()
            .Where(bs => bs.BranchId == branchId && bs.RevokedDate == null)
            .Select(bs => bs.SystemId)
            .ToListAsync();

        var revokedScreenIds = await _context.Set<SysBranchScreen>()
            .Where(bs => bs.BranchId == branchId)
            .Select(bs => bs.ScreenId)
            .ToListAsync();

        return await _context.SysScreens
            .Where(s => assignedSystemIds.Contains(s.SystemId)
                && s.IsActive
                && !revokedScreenIds.Contains(s.Id))
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<long>> GetRevokedScreenIdsAsync(long branchId)
    {
        return await _context.Set<SysBranchScreen>()
            .Where(bs => bs.BranchId == branchId)
            .Select(bs => bs.ScreenId)
            .ToListAsync();
    }

    public async Task RevokeScreenAsync(long branchId, long screenId, long? revokedBy)
    {
        var exists = await _context.Set<SysBranchScreen>()
            .AnyAsync(bs => bs.BranchId == branchId && bs.ScreenId == screenId);

        if (!exists)
        {
            _context.Set<SysBranchScreen>().Add(new SysBranchScreen
            {
                BranchId = branchId,
                ScreenId = screenId,
                RevokedBy = revokedBy,
                RevokedDate = DateTime.UtcNow,
                CreationUser = revokedBy?.ToString() ?? "system",
                CreationDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task AllowScreenAsync(long branchId, long screenId)
    {
        var record = await _context.Set<SysBranchScreen>()
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ScreenId == screenId);

        if (record != null)
        {
            _context.Set<SysBranchScreen>().Remove(record);
            await _context.SaveChangesAsync();
        }
    }
}
