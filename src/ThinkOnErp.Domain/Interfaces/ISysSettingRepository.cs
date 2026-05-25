using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysSettingRepository
{
    Task<List<SysSetting>> GetAllAsync();
    Task<SysSetting?> GetByCodeAsync(int settingCode);
    Task<SysSetting> AddAsync(SysSetting setting);
    Task UpdateAsync(SysSetting setting);
    Task DeleteAsync(int settingCode);
}
