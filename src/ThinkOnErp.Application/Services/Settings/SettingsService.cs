using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Settings;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Settings;

public class SettingsService : ISettingsService
{
    private readonly ISysSettingRepository _settingRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IInvWarehouseRepository _warehouseRepo;
    private readonly IGlAccountRepository? _accountRepo;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        ISysSettingRepository settingRepo,
        IBranchRepository branchRepo,
        IInvWarehouseRepository warehouseRepo,
        ILogger<SettingsService> logger,
        IGlAccountRepository? accountRepo = null)
    {
        _settingRepo = settingRepo;
        _branchRepo = branchRepo;
        _warehouseRepo = warehouseRepo;
        _logger = logger;
        _accountRepo = accountRepo;
    }

    public async Task<ApiResponse<CompanySettingsDto>> GetCompanySettingsAsync(CancellationToken ct = default)
    {
        var settings = await _settingRepo.GetAllAsync(branchId: 0, ct: ct);
        var map = settings.ToDictionary(s => s.SettingCode, s => s.SettingValue);

        var dto = new CompanySettingsDto();

        if (map.TryGetValue(1, out var p1)) dto.UploadFilesPath = p1;
        if (map.TryGetValue(2, out var p2) && long.TryParse(p2, out var s2)) dto.MaxFileSizeBytes = s2;
        if (map.TryGetValue(3, out var p3)) dto.AllowedExtensions = p3;
        if (map.TryGetValue(4, out var p4)) dto.LogsPath = p4;
        if (map.TryGetValue(5, out var p5)) dto.AuditFallbackPath = p5;
        if (map.TryGetValue(6, out var p6)) dto.LogosPath = p6;

        if (map.TryGetValue(SysSettingKeys.PosDefaultWarehouseId, out var whStr) && long.TryParse(whStr, out var whId))
        {
            dto.DefaultWarehouseId = whId;
            var wh = await _warehouseRepo.GetByIdAsync(whId, ct);
            dto.DefaultWarehouseName = wh?.WarehouseNameLocal ?? wh?.WarehouseNameEn;
        }

        if (map.TryGetValue(SysSettingKeys.PosDefaultCashAccount, out var cashAcc))
        {
            dto.DefaultCashAccount = cashAcc;
            dto.DefaultCashAccountName = await ResolveAccountNameAsync(cashAcc, ct);
        }

        if (map.TryGetValue(SysSettingKeys.PosDefaultExpenseAccount, out var expAcc))
        {
            dto.DefaultExpenseAccount = expAcc;
            dto.DefaultExpenseAccountName = await ResolveAccountNameAsync(expAcc, ct);
        }

        if (map.TryGetValue(SysSettingKeys.PosDefaultReceiptAccount, out var recAcc))
        {
            dto.DefaultReceiptAccount = recAcc;
            dto.DefaultReceiptAccountName = await ResolveAccountNameAsync(recAcc, ct);
        }

        if (map.TryGetValue(SysSettingKeys.PosStockDeductionMode, out var sdmStr) && int.TryParse(sdmStr, out var sdm))
        {
            dto.DefaultStockDeductionMode = sdm;
            dto.DefaultStockDeductionModeName = ResolveStockDeductionModeName(sdm);
        }

        if (map.TryGetValue(SysSettingKeys.PosShiftClosingMode, out var scmStr) && int.TryParse(scmStr, out var scm))
        {
            dto.DefaultShiftClosingMode = scm;
            dto.DefaultShiftClosingModeName = ResolveShiftClosingModeName(scm);
        }

        if (map.TryGetValue(SysSettingKeys.PosAllowNegativeStock, out var ansStr))
        {
            dto.DefaultAllowNegativeStock = ansStr == "1" || ansStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        if (map.TryGetValue(SysSettingKeys.PosPrintReceiptAutomatically, out var praStr))
        {
            dto.DefaultPrintReceiptAutomatically = praStr != "0" && !praStr.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        if (map.TryGetValue(SysSettingKeys.PosTaxApplicationMode, out var tamStr) && int.TryParse(tamStr, out var tam))
        {
            dto.DefaultTaxApplicationMode = tam;
            dto.DefaultTaxApplicationModeName = ResolveTaxApplicationModeName(tam);
        }

        if (map.TryGetValue(SysSettingKeys.PosBranchTaxPercent, out var btpStr) && decimal.TryParse(btpStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var btp))
        {
            dto.DefaultTaxPercent = btp;
        }

        if (map.TryGetValue(SysSettingKeys.PosPricesIncludeTax, out var pitStr))
        {
            dto.DefaultPricesIncludeTax = pitStr == "1" || pitStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return ApiResponse<CompanySettingsDto>.CreateSuccess(dto, "تم جلب إعدادات الشركة بنجاح", 200);
    }

    public async Task<ApiResponse<CompanySettingsDto>> UpdateCompanySettingsAsync(
        UpdateCompanySettingsDto dto,
        string username,
        CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<CompanySettingsDto>.CreateFailure("بيانات التعديل غير صالحة", statusCode: 400);

        if (!string.IsNullOrWhiteSpace(dto.UploadFilesPath))
            await _settingRepo.UpsertSettingAsync(0, 1, "FILE_UPLOAD_PATH", "مسار رفع الملفات", dto.UploadFilesPath.Trim(), "GENERAL", username, ct);

        if (dto.MaxFileSizeBytes.HasValue && dto.MaxFileSizeBytes.Value > 0)
            await _settingRepo.UpsertSettingAsync(0, 2, "FILE_MAX_SIZE", "أقصى حجم للملف بالبايت", dto.MaxFileSizeBytes.Value.ToString(), "GENERAL", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.AllowedExtensions))
            await _settingRepo.UpsertSettingAsync(0, 3, "FILE_ALLOWED_EXTS", "الامتدادات المسموح برفعها", dto.AllowedExtensions.Trim(), "GENERAL", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.LogsPath))
            await _settingRepo.UpsertSettingAsync(0, 4, "LOGS_PATH", "مسار حفظ سجلات النظام", dto.LogsPath.Trim(), "GENERAL", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.AuditFallbackPath))
            await _settingRepo.UpsertSettingAsync(0, 5, "AUDIT_FALLBACK_PATH", "مسار ملفات التدقيق الاحتياطية", dto.AuditFallbackPath.Trim(), "GENERAL", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.LogosPath))
            await _settingRepo.UpsertSettingAsync(0, 6, "LOGOS_PATH", "مسار شعارات الشركات والفروع", dto.LogosPath.Trim(), "GENERAL", username, ct);

        if (dto.DefaultWarehouseId.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosDefaultWarehouseId, "POS_DEFAULT_WAREHOUSE_ID", "المستودع الافتراضي لمبيعات الـ POS للشركة", dto.DefaultWarehouseId.Value.ToString(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultCashAccount))
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosDefaultCashAccount, "POS_DEFAULT_CASH_ACCOUNT", "صندوق الكاشير الافتراضي للشركة", dto.DefaultCashAccount.Trim(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultExpenseAccount))
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosDefaultExpenseAccount, "POS_DEFAULT_EXPENSE_ACCOUNT", "حساب المصروفات النثرية الافتراضي للشركة", dto.DefaultExpenseAccount.Trim(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultReceiptAccount))
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosDefaultReceiptAccount, "POS_DEFAULT_RECEIPT_ACCOUNT", "حساب المقبوضات الافتراضي للشركة", dto.DefaultReceiptAccount.Trim(), "POS", username, ct);

        if (dto.DefaultStockDeductionMode.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosStockDeductionMode, "POS_STOCK_DEDUCTION_MODE", "نمط خصم المخزون الافتراضي (1: فوري، 2: تجميعي بتقرير Z، 3: معطل)", dto.DefaultStockDeductionMode.Value.ToString(), "POS", username, ct);

        if (dto.DefaultShiftClosingMode.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosShiftClosingMode, "POS_SHIFT_CLOSING_MODE", "نمط إغلاق الوردية الافتراضي (1: إغلاق أعمى إلزامي، 2: عادي)", dto.DefaultShiftClosingMode.Value.ToString(), "POS", username, ct);

        if (dto.DefaultAllowNegativeStock.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosAllowNegativeStock, "POS_ALLOW_NEGATIVE_STOCK", "السماح بالبيع بالسالب في الـ POS", dto.DefaultAllowNegativeStock.Value ? "1" : "0", "POS", username, ct);

        if (dto.DefaultPrintReceiptAutomatically.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosPrintReceiptAutomatically, "POS_AUTO_PRINT_RECEIPT", "طباعة الإيصال آلياً عند إتمام الطلب", dto.DefaultPrintReceiptAutomatically.Value ? "1" : "0", "POS", username, ct);

        if (dto.DefaultTaxApplicationMode.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosTaxApplicationMode, "POS_TAX_APPLICATION_MODE", "نمط احتساب الضريبة الافتراضي (1: على مستوى الصنف، 2: على مستوى الفاتورة)", dto.DefaultTaxApplicationMode.Value.ToString(), "POS", username, ct);

        if (dto.DefaultTaxPercent.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosBranchTaxPercent, "POS_BRANCH_TAX_PERCENT", "نسبة الضريبة الافتراضية للشركة عند احتسابها على مستوى الفاتورة", dto.DefaultTaxPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "POS", username, ct);

        if (dto.DefaultPricesIncludeTax.HasValue)
            await _settingRepo.UpsertSettingAsync(0, SysSettingKeys.PosPricesIncludeTax, "POS_PRICES_INCLUDE_TAX", "هل الأسعار تشمل ضريبة القيمة المضافة افتراضياً للشركة", dto.DefaultPricesIncludeTax.Value ? "1" : "0", "POS", username, ct);

        if (dto.CustomSettings != null)
        {
            foreach (var kvp in dto.CustomSettings)
            {
                if (int.TryParse(kvp.Key, out var code))
                {
                    await _settingRepo.UpsertSettingAsync(0, code, $"CUSTOM_{code}", "إعداد مخصص للشركة", kvp.Value, "CUSTOM", username, ct);
                }
            }
        }

        _logger.LogInformation("Company settings updated by {User}", username);
        return await GetCompanySettingsAsync(ct);
    }

    public async Task<ApiResponse<List<BranchSettingsDto>>> GetAllBranchSettingsAsync(CancellationToken ct = default)
    {
        var branches = await _branchRepo.GetAllAsync();
        var list = new List<BranchSettingsDto>();

        foreach (var b in branches.Where(b => b.IsActive))
        {
            var res = await GetBranchSettingsAsync(b.Id, ct);
            if (res.Success && res.Data != null)
            {
                list.Add(res.Data);
            }
        }

        return ApiResponse<List<BranchSettingsDto>>.CreateSuccess(list, "تم جلب إعدادات الأفرع بنجاح", 200);
    }

    public async Task<ApiResponse<BranchSettingsDto>> GetBranchSettingsAsync(long branchId, CancellationToken ct = default)
    {
        var branch = await _branchRepo.GetByIdAsync(branchId);
        if (branch == null)
            return ApiResponse<BranchSettingsDto>.CreateFailure($"الفرع برقم {branchId} غير موجود", statusCode: 404);

        var effectiveMap = await _settingRepo.GetEffectiveSettingsForBranchAsync(branchId, ct);
        var branchOnlySettings = await _settingRepo.GetAllAsync(branchId: branchId, ct: ct);
        bool isCustomized = branchOnlySettings.Any();

        var dto = new BranchSettingsDto
        {
            BranchId = branchId,
            BranchName = branch.BranchNameLocal ?? branch.BranchNameEn ?? $"Branch {branchId}",
            IsCustomized = isCustomized
        };

        if (effectiveMap.TryGetValue(SysSettingKeys.PosDefaultWarehouseId, out var whStr) && long.TryParse(whStr, out var whId))
        {
            dto.DefaultWarehouseId = whId;
            var wh = await _warehouseRepo.GetByIdAsync(whId, ct);
            dto.DefaultWarehouseName = wh?.WarehouseNameLocal ?? wh?.WarehouseNameEn;
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosDefaultCashAccount, out var cashAcc))
        {
            dto.DefaultCashAccount = cashAcc;
            dto.DefaultCashAccountName = await ResolveAccountNameAsync(cashAcc, ct);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosDefaultExpenseAccount, out var expAcc))
        {
            dto.DefaultExpenseAccount = expAcc;
            dto.DefaultExpenseAccountName = await ResolveAccountNameAsync(expAcc, ct);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosDefaultReceiptAccount, out var recAcc))
        {
            dto.DefaultReceiptAccount = recAcc;
            dto.DefaultReceiptAccountName = await ResolveAccountNameAsync(recAcc, ct);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosStockDeductionMode, out var sdmStr) && int.TryParse(sdmStr, out var sdm))
        {
            dto.StockDeductionMode = sdm;
            dto.StockDeductionModeName = ResolveStockDeductionModeName(sdm);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosShiftClosingMode, out var scmStr) && int.TryParse(scmStr, out var scm))
        {
            dto.ShiftClosingMode = scm;
            dto.ShiftClosingModeName = ResolveShiftClosingModeName(scm);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosAllowNegativeStock, out var ansStr))
        {
            dto.AllowNegativeStock = ansStr == "1" || ansStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosDefaultPriceListId, out var plStr) && long.TryParse(plStr, out var plId))
        {
            dto.DefaultPriceListId = plId;
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosPrintReceiptAutomatically, out var praStr))
        {
            dto.PrintReceiptAutomatically = praStr != "0" && !praStr.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosTaxApplicationMode, out var tamStr) && int.TryParse(tamStr, out var tam))
        {
            dto.TaxApplicationMode = tam;
            dto.TaxApplicationModeName = ResolveTaxApplicationModeName(tam);
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosBranchTaxPercent, out var btpStr) && decimal.TryParse(btpStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var btp))
        {
            dto.BranchTaxPercent = btp;
        }

        if (effectiveMap.TryGetValue(SysSettingKeys.PosPricesIncludeTax, out var pitStr))
        {
            dto.PricesIncludeTax = pitStr == "1" || pitStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return ApiResponse<BranchSettingsDto>.CreateSuccess(dto, "تم جلب إعدادات الفرع بنجاح مع التوريث الهرمي", 200);
    }

    public async Task<ApiResponse<BranchSettingsDto>> UpdateBranchSettingsAsync(
        long branchId,
        UpdateBranchSettingsDto dto,
        string username,
        CancellationToken ct = default)
    {
        var branch = await _branchRepo.GetByIdAsync(branchId);
        if (branch == null)
            return ApiResponse<BranchSettingsDto>.CreateFailure($"الفرع برقم {branchId} غير موجود", statusCode: 404);

        if (dto == null)
            return ApiResponse<BranchSettingsDto>.CreateFailure("بيانات التعديل غير صالحة", statusCode: 400);

        if (dto.DefaultWarehouseId.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosDefaultWarehouseId, "POS_DEFAULT_WAREHOUSE_ID", "المستودع الافتراضي لمبيعات الـ POS للفرع", dto.DefaultWarehouseId.Value.ToString(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultCashAccount))
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosDefaultCashAccount, "POS_DEFAULT_CASH_ACCOUNT", "صندوق الكاشير الافتراضي للفرع", dto.DefaultCashAccount.Trim(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultExpenseAccount))
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosDefaultExpenseAccount, "POS_DEFAULT_EXPENSE_ACCOUNT", "حساب المصروفات النثرية الافتراضي للفرع", dto.DefaultExpenseAccount.Trim(), "POS", username, ct);

        if (!string.IsNullOrWhiteSpace(dto.DefaultReceiptAccount))
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosDefaultReceiptAccount, "POS_DEFAULT_RECEIPT_ACCOUNT", "حساب المقبوضات الافتراضي للفرع", dto.DefaultReceiptAccount.Trim(), "POS", username, ct);

        if (dto.StockDeductionMode.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosStockDeductionMode, "POS_STOCK_DEDUCTION_MODE", "نمط خصم المخزون للفرع", dto.StockDeductionMode.Value.ToString(), "POS", username, ct);

        if (dto.ShiftClosingMode.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosShiftClosingMode, "POS_SHIFT_CLOSING_MODE", "نمط إغلاق الوردية للفرع", dto.ShiftClosingMode.Value.ToString(), "POS", username, ct);

        if (dto.AllowNegativeStock.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosAllowNegativeStock, "POS_ALLOW_NEGATIVE_STOCK", "السماح بالبيع بالسالب للفرع", dto.AllowNegativeStock.Value ? "1" : "0", "POS", username, ct);

        if (dto.DefaultPriceListId.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosDefaultPriceListId, "POS_DEFAULT_PRICE_LIST_ID", "قائمة الأسعار الافتراضية للفرع", dto.DefaultPriceListId.Value.ToString(), "POS", username, ct);

        if (dto.PrintReceiptAutomatically.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosPrintReceiptAutomatically, "POS_AUTO_PRINT_RECEIPT", "طباعة الإيصال آلياً عند إتمام الطلب", dto.PrintReceiptAutomatically.Value ? "1" : "0", "POS", username, ct);

        if (dto.TaxApplicationMode.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosTaxApplicationMode, "POS_TAX_APPLICATION_MODE", "نمط احتساب الضريبة للفرع (1: على مستوى الصنف، 2: على مستوى الفاتورة)", dto.TaxApplicationMode.Value.ToString(), "POS", username, ct);

        if (dto.BranchTaxPercent.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosBranchTaxPercent, "POS_BRANCH_TAX_PERCENT", "نسبة الضريبة للفرع عند احتسابها على مستوى الفاتورة", dto.BranchTaxPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "POS", username, ct);

        if (dto.PricesIncludeTax.HasValue)
            await _settingRepo.UpsertSettingAsync(branchId, SysSettingKeys.PosPricesIncludeTax, "POS_PRICES_INCLUDE_TAX", "هل الأسعار تشمل ضريبة القيمة المضافة للفرع", dto.PricesIncludeTax.Value ? "1" : "0", "POS", username, ct);

        if (dto.CustomSettings != null)
        {
            foreach (var kvp in dto.CustomSettings)
            {
                if (int.TryParse(kvp.Key, out var code))
                {
                    await _settingRepo.UpsertSettingAsync(branchId, code, $"CUSTOM_{code}", "إعداد مخصص للفرع", kvp.Value, "CUSTOM", username, ct);
                }
            }
        }

        _logger.LogInformation("Settings for Branch {BranchId} updated by {User}", branchId, username);
        return await GetBranchSettingsAsync(branchId, ct);
    }

    public async Task<ApiResponse<bool>> ResetBranchSettingsAsync(long branchId, CancellationToken ct = default)
    {
        var branch = await _branchRepo.GetByIdAsync(branchId);
        if (branch == null)
            return ApiResponse<bool>.CreateFailure($"الفرع برقم {branchId} غير موجود", statusCode: 404);

        await _settingRepo.ResetBranchSettingsAsync(branchId, ct);
        _logger.LogInformation("Settings for Branch {BranchId} reset to Company defaults", branchId);
        return ApiResponse<bool>.CreateSuccess(true, "تم إعادة تعيين إعدادات الفرع لتتوافق مع إعدادات الشركة الافتراضية بنجاح", 200);
    }

    private async Task<string?> ResolveAccountNameAsync(string? accountCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountCode) || _accountRepo == null) return null;
        try
        {
            var acc = await _accountRepo.GetByCodeAsync(1, accountCode.Trim(), ct);
            return acc?.AccountNameLocal ?? acc?.AccountNameEn;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveStockDeductionModeName(int mode) => mode switch
    {
        1 => "RealTime (خصم فوري مع كل فاتورة)",
        2 => "Consolidated (خصم تجميعي بتقرير Z)",
        3 => "None (معطل)",
        _ => "RealTime"
    };

    private static string ResolveShiftClosingModeName(int mode) => mode switch
    {
        1 => "EnforceBlindClose (إغلاق أعمى إلزامي)",
        2 => "StandardOpenClose (إغلاق عادي)",
        _ => "EnforceBlindClose"
    };

    private static string ResolveTaxApplicationModeName(int mode) => mode switch
    {
        1 => "ItemLevel (على مستوى الصنف)",
        2 => "InvoiceLevel (على مستوى الفاتورة)",
        _ => "ItemLevel (على مستوى الصنف)"
    };
}
