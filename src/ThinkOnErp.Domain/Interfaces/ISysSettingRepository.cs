using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Unified Repository for Company-level and Branch-level settings with hierarchical fallback.
/// </summary>
public interface ISysSettingRepository
{
    /// <summary>
    /// Gets all active settings, optionally filtered by branch (branchId = 0 for company-only, null for all).
    /// </summary>
    Task<List<SysSetting>> GetAllAsync(long? branchId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific setting by code and branch (defaults to branchId = 0 for company default).
    /// </summary>
    Task<SysSetting?> GetByCodeAsync(int settingCode, long branchId = 0, CancellationToken ct = default);

    /// <summary>
    /// Resolves effective setting value for a branch: Checks branch override first; if not found, falls back to company default (BranchId = 0).
    /// </summary>
    Task<string?> GetEffectiveValueAsync(int settingCode, long branchId, CancellationToken ct = default);

    /// <summary>
    /// Resolves all effective settings for a branch as a dictionary of SettingCode -> Value, with company fallbacks.
    /// </summary>
    Task<Dictionary<int, string>> GetEffectiveSettingsForBranchAsync(long branchId, CancellationToken ct = default);

    /// <summary>
    /// Adds or updates a setting for a specific branch (or company if branchId = 0).
    /// </summary>
    Task UpsertSettingAsync(
        long branchId,
        int settingCode,
        string? settingKey,
        string settingDesc,
        string settingValue,
        string module,
        string username,
        CancellationToken ct = default);

    Task<SysSetting> AddAsync(SysSetting setting, CancellationToken ct = default);
    Task UpdateAsync(SysSetting setting, CancellationToken ct = default);
    Task DeleteAsync(int settingCode, long branchId = 0, CancellationToken ct = default);

    /// <summary>
    /// Resets all custom settings for a branch, reverting it back to company defaults.
    /// </summary>
    Task ResetBranchSettingsAsync(long branchId, CancellationToken ct = default);
}
