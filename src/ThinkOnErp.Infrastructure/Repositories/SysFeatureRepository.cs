using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SysFeatureRepository : ISysFeatureRepository
{
    private readonly OracleDbContext _context;
    public SysFeatureRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysFeature>> GetAllFeaturesAsync() =>
        await _context.SysFeatures.Where(f => f.IsActive).ToListAsync();

    public async Task<SysFeature?> GetFeatureByIdAsync(long featureId) =>
        await _context.SysFeatures.FindAsync(featureId);

    public async Task<long> CreateFeatureAsync(SysFeature feature)
    {
        _context.SysFeatures.Add(feature);
        await _context.SaveChangesAsync();
        return feature.Id;
    }

    public async Task UpdateFeatureAsync(SysFeature feature)
    {
        feature.UpdateDate = DateTime.Now;
        _context.SysFeatures.Update(feature);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteFeatureAsync(long featureId, string updateUser)
    {
        var feature = await _context.SysFeatures.FindAsync(featureId);
        if (feature != null)
        {
            feature.IsActive = false;
            feature.UpdateUser = updateUser;
            feature.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}
