using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SysSettingRepository : ISysSettingRepository
{
    private readonly OracleDbContext _context;

    public SysSettingRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<List<SysSetting>> GetAllAsync()
    {
        return await _context.SysSettings
            .OrderBy(s => s.SettingCode)
            .ToListAsync();
    }

    public async Task<SysSetting?> GetByCodeAsync(int settingCode)
    {
        return await _context.SysSettings
            .FirstOrDefaultAsync(s => s.SettingCode == settingCode);
    }

    public async Task<SysSetting> AddAsync(SysSetting setting)
    {
        _context.SysSettings.Add(setting);
        await _context.SaveChangesAsync();
        return setting;
    }

    public async Task UpdateAsync(SysSetting setting)
    {
        _context.SysSettings.Update(setting);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int settingCode)
    {
        var setting = await _context.SysSettings
            .FirstOrDefaultAsync(s => s.SettingCode == settingCode);
        if (setting != null)
        {
            _context.SysSettings.Remove(setting);
            await _context.SaveChangesAsync();
        }
    }
}
