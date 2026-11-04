using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class BranchFeatureRepository : IBranchFeatureRepository
{
    private readonly OracleDbContext _context;
    public BranchFeatureRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysFeature>> GetAvailableFeaturesAsync(long branchId, long screenId)
    {
        var assignedSystemIds = await _context.Set<SysBranchSystem>()
            .Where(bs => bs.BranchId == branchId && bs.RevokedDate == null)
            .Select(bs => bs.SystemId)
            .ToListAsync();

        if (assignedSystemIds.Count == 0)
        {
            return new List<SysFeature>();
        }

        var revokedFeatureIds = await _context.Set<SysBranchFeature>()
            .Where(bf => bf.BranchId == branchId && bf.ScreenId == screenId)
            .Select(bf => bf.FeatureId)
            .ToListAsync();

        return await _context.SysSystemScreenFeatures
            .Where(ssf => assignedSystemIds.Contains(ssf.SystemId) && ssf.ScreenId == screenId && ssf.IsActive)
            .Include(ssf => ssf.Feature)
            .Where(ssf => ssf.Feature != null && ssf.Feature.IsActive && !revokedFeatureIds.Contains(ssf.FeatureId))
            .Select(ssf => ssf.Feature!)
            .Distinct()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<(long ScreenId, long FeatureId)>> GetRevokedScreenFeaturesAsync(long branchId)
    {
        return await _context.Set<SysBranchFeature>()
            .Where(bf => bf.BranchId == branchId)
            .Select(bf => new ValueTuple<long, long>(bf.ScreenId, bf.FeatureId))
            .ToListAsync();
    }

    public async Task RevokeFeatureAsync(long branchId, long screenId, long featureId, long? revokedBy)
    {
        var exists = await _context.Set<SysBranchFeature>()
            .AnyAsync(bf => bf.BranchId == branchId && bf.ScreenId == screenId && bf.FeatureId == featureId);

        if (!exists)
        {
            _context.Set<SysBranchFeature>().Add(new SysBranchFeature
            {
                BranchId = branchId,
                ScreenId = screenId,
                FeatureId = featureId,
                RevokedBy = revokedBy,
                RevokedDate = DateTime.UtcNow,
                CreationUser = revokedBy?.ToString() ?? "system",
                CreationDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task AllowFeatureAsync(long branchId, long screenId, long featureId)
    {
        var record = await _context.Set<SysBranchFeature>()
            .FirstOrDefaultAsync(bf => bf.BranchId == branchId && bf.ScreenId == screenId && bf.FeatureId == featureId);

        if (record != null)
        {
            _context.Set<SysBranchFeature>().Remove(record);
            await _context.SaveChangesAsync();
        }
    }
}
