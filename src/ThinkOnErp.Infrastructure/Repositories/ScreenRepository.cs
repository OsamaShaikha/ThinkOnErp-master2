using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class ScreenRepository : IScreenRepository
{
    private readonly OracleDbContext _context;
    public ScreenRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysScreen>> GetAllScreensAsync() =>
        await _context.SysScreens
            .Include(s => s.SystemScreens)
            .ToListAsync();

    public async Task<List<SysScreen>> GetScreensBySystemIdAsync(long systemId) =>
        await _context.SysScreens
            .Include(s => s.SystemScreens)
            .Where(s => (s.SystemId == systemId || s.SystemScreens.Any(ss => ss.SystemId == systemId && ss.IsActive)) && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

    public async Task<SysScreen?> GetScreenByIdAsync(long screenId) =>
        await _context.SysScreens
            .Include(s => s.SystemScreens)
            .FirstOrDefaultAsync(s => s.Id == screenId);

    public async Task<List<long>> GetSystemIdsByScreenIdAsync(long screenId)
    {
        var systemsFromJoin = await _context.SysSystemScreens
            .Where(ss => ss.ScreenId == screenId && ss.IsActive)
            .Select(ss => ss.SystemId)
            .ToListAsync();

        if (systemsFromJoin.Count == 0)
        {
            var fallback = await _context.SysScreens
                .Where(s => s.Id == screenId && s.SystemId.HasValue)
                .Select(s => s.SystemId!.Value)
                .FirstOrDefaultAsync();

            if (fallback > 0)
                return new List<long> { fallback };
        }

        return systemsFromJoin;
    }

    public async Task SyncScreenSystemsAsync(long screenId, IEnumerable<long> systemIds, string username)
    {
        var targetSystemIds = systemIds?.Distinct().ToList() ?? new List<long>();
        var existingLinks = await _context.SysSystemScreens
            .Where(ss => ss.ScreenId == screenId)
            .ToListAsync();

        var existingMap = existingLinks.ToDictionary(l => l.SystemId);

        // Remove links that are not in target
        var removedSystemIds = new List<long>();
        foreach (var link in existingLinks.Where(l => !targetSystemIds.Contains(l.SystemId)))
        {
            _context.SysSystemScreens.Remove(link);
            removedSystemIds.Add(link.SystemId);
        }

        // Cascade delete feature links for systems removed from this screen
        if (removedSystemIds.Count > 0)
        {
            var detachedFeatureLinks = await _context.SysSystemScreenFeatures
                .Where(ssf => ssf.ScreenId == screenId && removedSystemIds.Contains(ssf.SystemId))
                .ToListAsync();
            if (detachedFeatureLinks.Count > 0)
            {
                _context.SysSystemScreenFeatures.RemoveRange(detachedFeatureLinks);
            }
        }

        // Add new links or reactivate inactive ones
        foreach (var sysId in targetSystemIds)
        {
            if (existingMap.TryGetValue(sysId, out var existing))
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.UpdateUser = username;
                    existing.UpdateDate = DateTime.UtcNow;
                }
            }
            else
            {
                _context.SysSystemScreens.Add(new SysSystemScreen
                {
                    ScreenId = screenId,
                    SystemId = sysId,
                    IsActive = true,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<long> CreateScreenAsync(SysScreen screen) =>
        await CreateScreenAsync(screen, null);

    public async Task<long> CreateScreenAsync(SysScreen screen, IEnumerable<long>? systemIds)
    {
        var list = systemIds?.Distinct().ToList() ?? new List<long>();
        if (list.Count > 0 && !screen.SystemId.HasValue)
        {
            screen.SystemId = list[0];
        }

        _context.SysScreens.Add(screen);
        await _context.SaveChangesAsync();

        if (list.Count > 0)
        {
            await SyncScreenSystemsAsync(screen.Id, list, screen.CreationUser);
        }

        return screen.Id;
    }

    public async Task UpdateScreenAsync(SysScreen screen) =>
        await UpdateScreenAsync(screen, null);

    public async Task UpdateScreenAsync(SysScreen screen, IEnumerable<long>? systemIds)
    {
        screen.UpdateDate = DateTime.Now;
        var list = systemIds?.Distinct().ToList();
        if (list != null && list.Count > 0 && !screen.SystemId.HasValue)
        {
            screen.SystemId = list[0];
        }

        _context.SysScreens.Update(screen);
        await _context.SaveChangesAsync();

        if (list != null)
        {
            await SyncScreenSystemsAsync(screen.Id, list, screen.UpdateUser ?? "system");
        }
    }

    public async Task DeleteScreenAsync(long screenId, string updateUser)
    {
        var screen = await _context.SysScreens.FindAsync(screenId);
        if (screen != null)
        {
            screen.IsActive = false;
            screen.UpdateUser = updateUser;
            screen.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}
