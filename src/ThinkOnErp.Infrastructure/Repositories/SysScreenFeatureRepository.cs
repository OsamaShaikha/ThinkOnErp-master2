using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Constants;
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
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
    }

    public async Task AssignFeaturesToScreenAsync(long screenId, List<long> featureIds)
    {
        var existing = await _context.Set<SysScreenFeature>()
            .Where(sf => sf.ScreenId == screenId)
            .ToListAsync();

        _context.Set<SysScreenFeature>().RemoveRange(existing);

        var distinctFeatureIds = featureIds?.Distinct().ToList() ?? new List<long>();
        var newAssignments = distinctFeatureIds.Select(featureId => new SysScreenFeature
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
        return await _context.SysSystemScreenFeatures
            .Where(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId && ssf.IsActive)
            .Include(ssf => ssf.Feature)
            .Where(ssf => ssf.Feature != null && ssf.Feature.IsActive)
            .Select(ssf => ssf.Feature!)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<SysFeature>> GetSystemLevelFeaturesAsync(long systemId)
    {
        return await GetFeaturesBySystemAndScreenAsync(systemId, null);
    }

    public async Task AssignFeaturesToSystemScreenAsync(long systemId, long? screenId, List<long> featureIds, string username)
    {
        if (screenId.HasValue)
        {
            var screenBelongsToSystem = await _context.SysSystemScreens
                .AnyAsync(ss => ss.SystemId == systemId && ss.ScreenId == screenId.Value && ss.IsActive)
                || await _context.SysScreens
                .AnyAsync(s => s.Id == screenId.Value && s.SystemId == systemId && s.IsActive);

            if (!screenBelongsToSystem)
            {
                throw new InvalidOperationException(ErrorCodes.ScreenNotInSystem);
            }
        }

        var distinctFeatureIds = featureIds?.Distinct().ToList() ?? new List<long>();

        var existingRecords = await _context.SysSystemScreenFeatures
            .Where(ssf => ssf.SystemId == systemId && ssf.ScreenId == screenId)
            .ToListAsync();

        var existingMap = existingRecords.ToDictionary(r => r.FeatureId);

        foreach (var rec in existingRecords)
        {
            if (!distinctFeatureIds.Contains(rec.FeatureId))
            {
                _context.SysSystemScreenFeatures.Remove(rec);
            }
        }

        foreach (var featureId in distinctFeatureIds)
        {
            if (existingMap.TryGetValue(featureId, out var existing))
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.UpdateUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username;
                    existing.UpdateDate = DateTime.UtcNow;
                }
            }
            else
            {
                _context.SysSystemScreenFeatures.Add(new SysSystemScreenFeature
                {
                    SystemId = systemId,
                    ScreenId = screenId,
                    FeatureId = featureId,
                    IsActive = true,
                    CreationUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

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
