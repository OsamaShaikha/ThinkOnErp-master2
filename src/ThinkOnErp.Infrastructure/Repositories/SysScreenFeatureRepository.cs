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
}
