using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

/// <summary>
/// PosBranchSettingRepository adapter: Provides branch-specific settings by delegating to the unified SYS_SETTINGS repository with hierarchical inheritance.
/// </summary>
public class PosBranchSettingRepository : IPosBranchSettingRepository
{
    private readonly ISysSettingRepository _settingRepo;
    private readonly OracleDbContext _context;

    public PosBranchSettingRepository(ISysSettingRepository settingRepo, OracleDbContext context)
    {
        _settingRepo = settingRepo;
        _context = context;
    }

    public async Task<PosBranchSetting?> GetByBranchIdAsync(long branchId, CancellationToken ct = default)
    {
        var cashAcc = await _settingRepo.GetEffectiveValueAsync(SysSettingKeys.PosDefaultCashAccount, branchId, ct);
        var expAcc = await _settingRepo.GetEffectiveValueAsync(SysSettingKeys.PosDefaultExpenseAccount, branchId, ct);
        var recAcc = await _settingRepo.GetEffectiveValueAsync(SysSettingKeys.PosDefaultReceiptAccount, branchId, ct);
        var whVal = await _settingRepo.GetEffectiveValueAsync(SysSettingKeys.PosDefaultWarehouseId, branchId, ct);
        long? whId = long.TryParse(whVal, out var parsedWh) ? parsedWh : null;

        var branch = await _context.SysBranches.FirstOrDefaultAsync(b => b.Id == branchId, ct);

        return new PosBranchSetting
        {
            Id = branchId,
            BranchId = branchId,
            DefaultCashAccount = cashAcc,
            DefaultExpenseAccount = expAcc,
            DefaultReceiptAccount = recAcc,
            DefaultWarehouseId = whId,
            Branch = branch,
            IsActive = true
        };
    }

    public async Task<IReadOnlyList<PosBranchSetting>> GetAllAsync(CancellationToken ct = default)
    {
        var branches = await _context.SysBranches.Where(b => b.IsActive).ToListAsync(ct);
        var list = new List<PosBranchSetting>();
        foreach (var b in branches)
        {
            var setting = await GetByBranchIdAsync(b.Id, ct);
            if (setting != null) list.Add(setting);
        }
        return list;
    }

    public async Task AddAsync(PosBranchSetting setting, CancellationToken ct = default)
    {
        await UpdateAsync(setting, ct);
    }

    public async Task UpdateAsync(PosBranchSetting setting, CancellationToken ct = default)
    {
        if (setting == null || setting.BranchId <= 0) return;

        var user = setting.UpdateUser ?? setting.CreationUser ?? "SYSTEM";

        if (!string.IsNullOrWhiteSpace(setting.DefaultCashAccount))
            await _settingRepo.UpsertSettingAsync(setting.BranchId, SysSettingKeys.PosDefaultCashAccount, "POS_DEFAULT_CASH_ACCOUNT", "صندوق الكاشير الافتراضي للفرع", setting.DefaultCashAccount.Trim(), "POS", user, ct);

        if (!string.IsNullOrWhiteSpace(setting.DefaultExpenseAccount))
            await _settingRepo.UpsertSettingAsync(setting.BranchId, SysSettingKeys.PosDefaultExpenseAccount, "POS_DEFAULT_EXPENSE_ACCOUNT", "حساب المصروفات النثرية الافتراضي للفرع", setting.DefaultExpenseAccount.Trim(), "POS", user, ct);

        if (!string.IsNullOrWhiteSpace(setting.DefaultReceiptAccount))
            await _settingRepo.UpsertSettingAsync(setting.BranchId, SysSettingKeys.PosDefaultReceiptAccount, "POS_DEFAULT_RECEIPT_ACCOUNT", "حساب المقبوضات الافتراضي للفرع", setting.DefaultReceiptAccount.Trim(), "POS", user, ct);

        if (setting.DefaultWarehouseId.HasValue && setting.DefaultWarehouseId.Value > 0)
            await _settingRepo.UpsertSettingAsync(setting.BranchId, SysSettingKeys.PosDefaultWarehouseId, "POS_DEFAULT_WAREHOUSE_ID", "المستودع الافتراضي لمبيعات نقطة البيع للفرع", setting.DefaultWarehouseId.Value.ToString(), "POS", user, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
