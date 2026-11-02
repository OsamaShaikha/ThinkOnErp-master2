using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SysScreenFeatureRepository : ISysScreenFeatureRepository
{
    private readonly OracleDbContext _context;
    public SysScreenFeatureRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysFeature>> GetFeaturesByScreenIdAsync(long screenId)
    {
        return await _context.Set<SysScreenFeature>()
            .Where(sf => sf.ScreenId == screenId)
            .Include(sf => sf.Feature)
            .Where(sf => sf.Feature!.IsActive)
            .Select(sf => sf.Feature!)
            .ToListAsync();
    }

    public async Task AssignFeaturesToScreenAsync(long screenId, List<long> featureIds)
    {
        var existing = await _context.Set<SysScreenFeature>()
            .Where(sf => sf.ScreenId == screenId)
            .ToListAsync();

        _context.Set<SysScreenFeature>().RemoveRange(existing);

        var newAssignments = featureIds.Select(featureId => new SysScreenFeature
        {
            ScreenId = screenId,
            FeatureId = featureId
        });
        _context.Set<SysScreenFeature>().AddRange(newAssignments);

        await _context.SaveChangesAsync();
    }

    public async Task RemoveFeatureFromScreenAsync(long screenId, long featureId)
    {
        var assignment = await _context.Set<SysScreenFeature>()
            .FirstOrDefaultAsync(sf => sf.ScreenId == screenId && sf.FeatureId == featureId);

        if (assignment != null)
        {
            _context.Set<SysScreenFeature>().Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SysFeature>> GetFeaturesBySystemAndScreenAsync(long systemId, long? screenId)
    {
        var features = await _context.SysSystemScreenFeatures
            .Where(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId && ssf.IsActive)
            .Include(ssf => ssf.Feature)
            .Where(ssf => ssf.Feature != null && ssf.Feature.IsActive)
            .Select(ssf => ssf.Feature!)
            .ToListAsync();

        // Fallback to legacy SYS_SCREEN_FEATURE if no specific system-screen mapping is configured yet
        if (features.Count == 0 && screenId.HasValue)
        {
            var hasConfiguredMapping = await _context.SysSystemScreenFeatures
                .AnyAsync(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId);

            if (!hasConfiguredMapping)
            {
                return await GetFeaturesByScreenIdAsync(screenId.Value);
            }
        }

        return features;
    }

    public async Task<List<SysFeature>> GetSystemLevelFeaturesAsync(long systemId)
    {
        return await GetFeaturesBySystemAndScreenAsync(systemId, null);
    }

    public async Task AssignFeaturesToSystemScreenAsync(long systemId, long? screenId, List<long> featureIds, string username)
    {
        var existing = await _context.SysSystemScreenFeatures
            .Where(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId)
            .ToListAsync();

        _context.SysSystemScreenFeatures.RemoveRange(existing);

        var distinctFeatureIds = featureIds.Distinct().ToList();
        var newAssignments = distinctFeatureIds.Select(featureId => new SysSystemScreenFeature
        {
            SystemId = systemId,
            ScreenId = screenId,
            FeatureId = featureId,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username,
            CreationDate = DateTime.UtcNow
        });

        _context.SysSystemScreenFeatures.AddRange(newAssignments);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveFeatureFromSystemScreenAsync(long systemId, long? screenId, long featureId)
    {
        var assignment = await _context.SysSystemScreenFeatures
            .FirstOrDefaultAsync(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId && ssf.FeatureId == featureId);

        if (assignment != null)
        {
            _context.SysSystemScreenFeatures.Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }
}
