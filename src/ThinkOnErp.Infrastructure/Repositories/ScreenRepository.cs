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
        await _context.SysScreens.Where(s => s.IsActive).ToListAsync();

    public async Task<List<SysScreen>> GetScreensBySystemIdAsync(long systemId) =>
        await _context.SysScreens.Where(s => s.SystemId == systemId && s.IsActive).ToListAsync();

    public async Task<SysScreen?> GetScreenByIdAsync(long screenId) =>
        await _context.SysScreens.FindAsync(screenId);

    public async Task<long> CreateScreenAsync(SysScreen screen)
    {
        _context.SysScreens.Add(screen);
        await _context.SaveChangesAsync();
        return screen.Id;
    }

    public async Task UpdateScreenAsync(SysScreen screen)
    {
        screen.UpdateDate = DateTime.Now;
        _context.SysScreens.Update(screen);
        await _context.SaveChangesAsync();
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