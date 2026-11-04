using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Settings;

/// <summary>
/// Branch settings model: Aggregates POS, Inventory, and Accounting preferences for a specific branch.
/// </summary>
public sealed class BranchSettingsDto
{
    public long BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    // Warehouse Settings
    public long? DefaultWarehouseId { get; set; }
    public string? DefaultWarehouseName { get; set; }

    // Accounting & POS Cashier Accounts
    public string? DefaultCashAccount { get; set; }
    public string? DefaultCashAccountName { get; set; }

    public string? DefaultExpenseAccount { get; set; }
    public string? DefaultExpenseAccountName { get; set; }

    public string? DefaultReceiptAccount { get; set; }
    public string? DefaultReceiptAccountName { get; set; }

    // POS Operating Behaviors
    public int StockDeductionMode { get; set; } = 1;
    public string StockDeductionModeName { get; set; } = "RealTime";

    public int ShiftClosingMode { get; set; } = 1;
    public string ShiftClosingModeName { get; set; } = "EnforceBlindClose";

    public bool AllowNegativeStock { get; set; }

    public long? DefaultPriceListId { get; set; }
    public string? DefaultPriceListName { get; set; }

    public bool PrintReceiptAutomatically { get; set; } = true;

    // Tax Settings
    /// <summary>
    /// 1 = ItemLevel (حساب الضريبة لكل صنف على حدة)، 2 = InvoiceLevel (تطبيق نسبة موحدة على إجمالي الفاتورة)
    /// </summary>
    public int TaxApplicationMode { get; set; } = 0;
    public string TaxApplicationModeName { get; set; } = "ItemLevel";
    public decimal? BranchTaxPercent { get; set; }

    /// <summary>
    /// هل الأسعار تشمل ضريبة القيمة المضافة افتراضياً للفرع؟
    /// </summary>
    public bool PricesIncludeTax { get; set; } = false;

    /// <summary>
    /// Indicates whether this branch has explicit overrides configured or is inheriting company defaults.
    /// </summary>
    public bool IsCustomized { get; set; }

    /// <summary>
    /// Additional dynamic settings for this branch.
    /// </summary>
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}

/// <summary>
/// Payload to update settings for a specific branch.
/// </summary>
public sealed class UpdateBranchSettingsDto
{
    public long? DefaultWarehouseId { get; set; }
    public string? DefaultCashAccount { get; set; }
    public string? DefaultExpenseAccount { get; set; }
    public string? DefaultReceiptAccount { get; set; }
    public int? StockDeductionMode { get; set; }
    public int? ShiftClosingMode { get; set; }
    public bool? AllowNegativeStock { get; set; }
    public long? DefaultPriceListId { get; set; }
    public bool? PrintReceiptAutomatically { get; set; }
    public int? TaxApplicationMode { get; set; }
    public decimal? BranchTaxPercent { get; set; }
    public bool? PricesIncludeTax { get; set; }
    public Dictionary<string, string>? CustomSettings { get; set; }
}

/// <summary>
/// Company-level settings: Represents global defaults inherited by all branches unless overridden.
/// </summary>
public sealed class CompanySettingsDto
{
    public string UploadFilesPath { get; set; } = "/THINKON_FILES/UPLOADS/";
    public long MaxFileSizeBytes { get; set; } = 52428800;
    public string AllowedExtensions { get; set; } = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.jpg,.jpeg,.png,.gif,.zip,.rar,.7z";
    public string LogsPath { get; set; } = "/THINKON_FILES/LOGS/";
    public string AuditFallbackPath { get; set; } = "/THINKON_FILES/LOGS/audit-fallback/";
    public string LogosPath { get; set; } = "/THINKON_FILES/LOGOS/";

    // Company-wide POS Defaults
    public long? DefaultWarehouseId { get; set; }
    public string? DefaultWarehouseName { get; set; }
    public string? DefaultCashAccount { get; set; }
    public string? DefaultCashAccountName { get; set; }
    public string? DefaultExpenseAccount { get; set; }
    public string? DefaultExpenseAccountName { get; set; }
    public string? DefaultReceiptAccount { get; set; }
    public string? DefaultReceiptAccountName { get; set; }

    public int DefaultStockDeductionMode { get; set; } = 1;
    public string DefaultStockDeductionModeName { get; set; } = "RealTime";

    public int DefaultShiftClosingMode { get; set; } = 1;
    public string DefaultShiftClosingModeName { get; set; } = "EnforceBlindClose";

    public bool DefaultAllowNegativeStock { get; set; }
    public bool DefaultPrintReceiptAutomatically { get; set; } = true;

    public int DefaultTaxApplicationMode { get; set; } = 0;
    public string DefaultTaxApplicationModeName { get; set; } = "ItemLevel";
    public decimal? DefaultTaxPercent { get; set; }

    /// <summary>
    /// هل الأسعار تشمل ضريبة القيمة المضافة افتراضياً على مستوى المنشأة؟
    /// </summary>
    public bool DefaultPricesIncludeTax { get; set; } = false;

    public Dictionary<string, string> CustomSettings { get; set; } = new();
}

/// <summary>
/// Payload to update company-level global settings.
/// </summary>
public sealed class UpdateCompanySettingsDto
{
    public string? UploadFilesPath { get; set; }
    public long? MaxFileSizeBytes { get; set; }
    public string? AllowedExtensions { get; set; }
    public string? LogsPath { get; set; }
    public string? AuditFallbackPath { get; set; }
    public string? LogosPath { get; set; }

    public long? DefaultWarehouseId { get; set; }
    public string? DefaultCashAccount { get; set; }
    public string? DefaultExpenseAccount { get; set; }
    public string? DefaultReceiptAccount { get; set; }
    public int? DefaultStockDeductionMode { get; set; }
    public int? DefaultShiftClosingMode { get; set; }
    public bool? DefaultAllowNegativeStock { get; set; }
    public bool? DefaultPrintReceiptAutomatically { get; set; }
    public int? DefaultTaxApplicationMode { get; set; }
    public decimal? DefaultTaxPercent { get; set; }
    public bool? DefaultPricesIncludeTax { get; set; }

    public Dictionary<string, string>? CustomSettings { get; set; }
}
