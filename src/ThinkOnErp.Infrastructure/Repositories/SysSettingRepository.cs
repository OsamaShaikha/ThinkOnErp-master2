using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<List<SysSetting>> GetAllAsync(long? branchId = null, CancellationToken ct = default)
    {
        var query = _context.SysSettings.AsNoTracking().Where(s => s.IsActive);
        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(s => s.BranchId)
            .ThenBy(s => s.SettingCode)
            .ToListAsync(ct);
    }

    public async Task<SysSetting?> GetByCodeAsync(int settingCode, long branchId = 0, CancellationToken ct = default)
    {
        // 1. Try branch-specific setting
        if (branchId > 0)
        {
            var branchSetting = await _context.SysSettings
                .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SettingCode == settingCode && s.IsActive, ct);

            if (branchSetting != null)
                return branchSetting;
        }

        // 2. Fallback to Company-wide setting (BranchId = 0)
        return await _context.SysSettings
            .FirstOrDefaultAsync(s => s.BranchId == 0 && s.SettingCode == settingCode && s.IsActive, ct);
    }

    public async Task<string?> GetEffectiveValueAsync(int settingCode, long branchId, CancellationToken ct = default)
    {
        // 1. Try branch-specific setting
        if (branchId > 0)
        {
            var branchSetting = await _context.SysSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SettingCode == settingCode && s.IsActive, ct);

            if (branchSetting != null && !string.IsNullOrWhiteSpace(branchSetting.SettingValue))
                return branchSetting.SettingValue;
        }

        // 2. Fallback to Company-wide default (BranchId = 0)
        var companySetting = await _context.SysSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BranchId == 0 && s.SettingCode == settingCode && s.IsActive, ct);

        return companySetting?.SettingValue;
    }

    public async Task<Dictionary<int, string>> GetEffectiveSettingsForBranchAsync(long branchId, CancellationToken ct = default)
    {
        // Load Company defaults first
        var companySettings = await _context.SysSettings
            .AsNoTracking()
            .Where(s => s.BranchId == 0 && s.IsActive)
            .ToDictionaryAsync(s => s.SettingCode, s => s.SettingValue, ct);

        // If specific branch, overlay branch overrides
        if (branchId > 0)
        {
            var branchSettings = await _context.SysSettings
                .AsNoTracking()
                .Where(s => s.BranchId == branchId && s.IsActive)
                .ToListAsync(ct);

            foreach (var bs in branchSettings)
            {
                if (!string.IsNullOrWhiteSpace(bs.SettingValue))
                {
                    companySettings[bs.SettingCode] = bs.SettingValue;
                }
            }
        }

        return companySettings;
    }

    public async Task UpsertSettingAsync(
        long branchId,
        int settingCode,
        string? settingKey,
        string settingDesc,
        string settingValue,
        string module,
        string username,
        CancellationToken ct = default)
    {
        var existing = await _context.SysSettings
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SettingCode == settingCode, ct);

        if (existing != null)
        {
            existing.SettingValue = settingValue;
            if (!string.IsNullOrWhiteSpace(settingDesc)) existing.SettingDesc = settingDesc;
            if (!string.IsNullOrWhiteSpace(settingKey)) existing.SettingKey = settingKey;
            if (!string.IsNullOrWhiteSpace(module)) existing.Module = module;
            existing.IsActive = true;
            existing.UpdateUser = username;
            existing.UpdateDate = DateTime.UtcNow;
            _context.SysSettings.Update(existing);
        }
        else
        {
            var entity = new SysSetting
            {
                BranchId = branchId,
                SettingCode = settingCode,
                SettingKey = settingKey,
                SettingDesc = settingDesc,
                SettingValue = settingValue,
                Module = module,
                IsActive = true,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            };
            await _context.SysSettings.AddAsync(entity, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<SysSetting> AddAsync(SysSetting setting, CancellationToken ct = default)
    {
        _context.SysSettings.Add(setting);
        await _context.SaveChangesAsync(ct);
        return setting;
    }

    public async Task UpdateAsync(SysSetting setting, CancellationToken ct = default)
    {
        _context.SysSettings.Update(setting);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int settingCode, long branchId = 0, CancellationToken ct = default)
    {
        var setting = await _context.SysSettings
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.SettingCode == settingCode, ct);

        if (setting != null)
        {
            _context.SysSettings.Remove(setting);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task ResetBranchSettingsAsync(long branchId, CancellationToken ct = default)
    {
        if (branchId <= 0) return;

        var branchSettings = await _context.SysSettings
            .Where(s => s.BranchId == branchId)
            .ToListAsync(ct);

        if (branchSettings.Count > 0)
        {
            _context.SysSettings.RemoveRange(branchSettings);
            await _context.SaveChangesAsync(ct);
        }
    }
}
