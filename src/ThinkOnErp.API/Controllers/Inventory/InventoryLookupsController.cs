using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// ثوابت وقوائم تعريفات المخزون المنسدلة (Inventory Lookups API)
/// يتيح الوصول السريع لأنواع حركات المخزون، هياكل التجميع (BOM)، طرق التكلفة، وأنواع الأصناف
/// </summary>
[ApiController]
[Route("api/inventory/lookups")]
[Route("api/inventory")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
public class InventoryLookupsController : ControllerBase
{
    private readonly ISysCodeRepository _sysCodeRepo;
    private readonly ITrxTypeRepository _trxTypeRepo;

    public InventoryLookupsController(ISysCodeRepository sysCodeRepo, ITrxTypeRepository trxTypeRepo)
    {
        _sysCodeRepo = sysCodeRepo;
        _trxTypeRepo = trxTypeRepo;
    }

    private static List<SysCodeLookupDto> MapToLookupDtos(IEnumerable<SysCode> rawCodes, int? lang)
    {
        return rawCodes
            .Where(c => c.CodeMnr > 0)
            .GroupBy(c => c.CodeMnr)
            .Select(g =>
            {
                var ar = g.FirstOrDefault(x => x.CodeLang == 1);
                var en = g.FirstOrDefault(x => x.CodeLang == 2);
                var first = g.First();

                var arDesc = ar?.CodeDesc ?? first.CodeDesc;
                var enDesc = en?.CodeDesc ?? first.CodeDesc;
                var val = !string.IsNullOrEmpty(first.CodeValue)
                    ? first.CodeValue
                    : (en?.CodeValue ?? ar?.CodeValue ?? string.Empty);

                var isEnglish = lang == 2;
                var localizedName = isEnglish
                    ? (!string.IsNullOrEmpty(enDesc) ? enDesc : arDesc)
                    : (!string.IsNullOrEmpty(arDesc) ? arDesc : enDesc);

                return new SysCodeLookupDto
                {
                    Code = g.Key,
                    Value = val,
                    NameAr = arDesc,
                    NameEn = enDesc,
                    Name = localizedName,
                    IsActive = first.IsActive
                };
            })
            .OrderBy(x => x.Code)
            .ToList();
    }

    private async Task<List<SysCodeLookupDto>> GetLookupsByMgrAsync(int mgr, int? lang)
    {
        var raw = await _sysCodeRepo.GetActiveByCodeMgrAsync(mgr);
        if (raw.Count > 0)
        {
            return MapToLookupDtos(raw, lang);
        }

        // Fallback static definitions if database table has not been loaded
        return GetFallbackLookups(mgr, lang);
    }

    private static List<SysCodeLookupDto> GetFallbackLookups(int mgr, int? lang)
    {
        var isEnglish = lang == 2;
        return mgr switch
        {
            SysCodeKeys.ItemTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "Stock", NameAr = "مخزني", NameEn = "Stock", Name = isEnglish ? "Stock" : "مخزني", IsActive = 1 },
                new() { Code = 2, Value = "NonStock", NameAr = "غير مخزني", NameEn = "Non-Stock", Name = isEnglish ? "Non-Stock" : "غير مخزني", IsActive = 1 },
                new() { Code = 3, Value = "Service", NameAr = "خدمي", NameEn = "Service", Name = isEnglish ? "Service" : "خدمي", IsActive = 1 },
                new() { Code = 4, Value = "Kit", NameAr = "طقم / كيت", NameEn = "Kit", Name = isEnglish ? "Kit" : "طقم / كيت", IsActive = 1 },
                new() { Code = 5, Value = "Assembly", NameAr = "تجميعي / تصنيعي", NameEn = "Assembly", Name = isEnglish ? "Assembly" : "تجميعي / تصنيعي", IsActive = 1 },
            },
            SysCodeKeys.CostingMethods.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "WeightedAverage", NameAr = "المتوسط المرجح", NameEn = "Weighted Average", Name = isEnglish ? "Weighted Average" : "المتوسط المرجح", IsActive = 1 },
                new() { Code = 2, Value = "Fifo", NameAr = "الوارد أولاً صادر أولاً (FIFO)", NameEn = "First In, First Out (FIFO)", Name = isEnglish ? "First In, First Out (FIFO)" : "الوارد أولاً صادر أولاً (FIFO)", IsActive = 1 },
                new() { Code = 3, Value = "SpecificId", NameAr = "التمييز المحدد / التكلفة الفعلية", NameEn = "Specific Identification", Name = isEnglish ? "Specific Identification" : "التمييز المحدد / التكلفة الفعلية", IsActive = 1 },
                new() { Code = 4, Value = "Standard", NameAr = "التكلفة المعيارية", NameEn = "Standard Cost", Name = isEnglish ? "Standard Cost" : "التكلفة المعيارية", IsActive = 1 },
            },
            SysCodeKeys.BomTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "SalesKit", NameAr = "طقم مبيعات (Sales Kit)", NameEn = "Sales Kit", Name = isEnglish ? "Sales Kit" : "طقم مبيعات (Sales Kit)", IsActive = 1 },
                new() { Code = 2, Value = "ProductionAssembly", NameAr = "تجميع إنتاجي (Production Assembly)", NameEn = "Production Assembly", Name = isEnglish ? "Production Assembly" : "تجميع إنتاجي (Production Assembly)", IsActive = 1 },
                new() { Code = 3, Value = "Disassembly", NameAr = "تفكيك (Disassembly)", NameEn = "Disassembly", Name = isEnglish ? "Disassembly" : "تفكيك (Disassembly)", IsActive = 1 },
            },
            SysCodeKeys.StockTransactionTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1001, Value = "LOCAL_CASH_SALES", NameAr = "مبيعات محلية نقدية", NameEn = "Local Cash Sales", Name = isEnglish ? "Local Cash Sales" : "مبيعات محلية نقدية", IsActive = 1 },
                new() { Code = 1002, Value = "LOCAL_CREDIT_SALES", NameAr = "مبيعات محلية ذمم/آجل", NameEn = "Local Credit Sales", Name = isEnglish ? "Local Credit Sales" : "مبيعات محلية ذمم/آجل", IsActive = 1 },
                new() { Code = 1003, Value = "EXPORT_SALES", NameAr = "مبيعات تصدير خارجي", NameEn = "Export Sales", Name = isEnglish ? "Export Sales" : "مبيعات تصدير خارجي", IsActive = 1 },
                new() { Code = 1004, Value = "POS_SALES", NameAr = "مبيعات نقاط بيع تجزئة", NameEn = "POS Retail Sales", Name = isEnglish ? "POS Retail Sales" : "مبيعات نقاط بيع تجزئة", IsActive = 1 },
                new() { Code = 1501, Value = "SALES_RETURN_RESTOCK", NameAr = "مردود مبيعات سليم", NameEn = "Sales Return (Restock)", Name = isEnglish ? "Sales Return (Restock)" : "مردود مبيعات سليم", IsActive = 1 },
                new() { Code = 1502, Value = "SALES_RETURN_SCRAP", NameAr = "مردود مبيعات تالف/هالك", NameEn = "Sales Return (Scrap)", Name = isEnglish ? "Sales Return (Scrap)" : "مردود مبيعات تالف/هالك", IsActive = 1 },
                new() { Code = 2001, Value = "LOCAL_PURCHASE", NameAr = "مشتريات محلية بضاعة", NameEn = "Local Purchases", Name = isEnglish ? "Local Purchases" : "مشتريات محلية بضاعة", IsActive = 1 },
                new() { Code = 2003, Value = "IMPORT_PURCHASE", NameAr = "مشتريات استيراد خارجي", NameEn = "Import Purchases", Name = isEnglish ? "Import Purchases" : "مشتريات استيراد خارجي", IsActive = 1 },
                new() { Code = 2501, Value = "PURCHASE_RETURN", NameAr = "مردود مشتريات لمورد", NameEn = "Purchase Return", Name = isEnglish ? "Purchase Return" : "مردود مشتريات لمورد", IsActive = 1 },
                new() { Code = 3001, Value = "OPENING_STOCK", NameAr = "سند إدخال رصيد افتتاحي", NameEn = "Opening Stock Inbound", Name = isEnglish ? "Opening Stock Inbound" : "سند إدخال رصيد افتتاحي", IsActive = 1 },
                new() { Code = 3002, Value = "STOCK_SURPLUS", NameAr = "سند إدخال تسوية زيادة", NameEn = "Stock Count Surplus", Name = isEnglish ? "Stock Count Surplus" : "سند إدخال تسوية زيادة", IsActive = 1 },
                new() { Code = 3003, Value = "FREE_SAMPLES_IN", NameAr = "سند إدخال عينات وهدايا", NameEn = "Free Samples Inbound", Name = isEnglish ? "Free Samples Inbound" : "سند إدخال عينات وهدايا", IsActive = 1 },
                new() { Code = 3011, Value = "STOCK_SHORTAGE", NameAr = "سند إخراج تسوية عجز", NameEn = "Stock Count Shortage", Name = isEnglish ? "Stock Count Shortage" : "سند إخراج تسوية عجز", IsActive = 1 },
                new() { Code = 3012, Value = "SCRAP_WRITEOFF", NameAr = "سند إخراج إتلاف وهالك", NameEn = "Scrap & Damage Write-Off", Name = isEnglish ? "Scrap & Damage Write-Off" : "سند إخراج إتلاف وهالك", IsActive = 1 },
                new() { Code = 3014, Value = "INTERNAL_USE", NameAr = "سند إخراج استهلاك أقسام", NameEn = "Internal Consumption", Name = isEnglish ? "Internal Consumption" : "سند إخراج استهلاك أقسام", IsActive = 1 },
                new() { Code = 3501, Value = "INTERNAL_TRANSFER", NameAr = "تحويل بين مستودعات", NameEn = "Internal Warehouse Transfer", Name = isEnglish ? "Internal Warehouse Transfer" : "تحويل بين مستودعات", IsActive = 1 },
                new() { Code = 4001, Value = "SALES_QUOTATION", NameAr = "عرض أسعار لعميل", NameEn = "Sales Quotation", Name = isEnglish ? "Sales Quotation" : "عرض أسعار لعميل", IsActive = 1 },
                new() { Code = 4004, Value = "PURCHASE_ORDER", NameAr = "أمر شراء لمورد", NameEn = "Purchase Order", Name = isEnglish ? "Purchase Order" : "أمر شراء لمورد", IsActive = 1 },
            },
            _ => new List<SysCodeLookupDto>()
        };
    }

    /// <summary>
    /// استرجاع كافة ثوابت وقوائم المخزون دفعة واحدة لتغذية كاش واجهة المستخدم
    /// </summary>
    [HttpGet("all")]
    [HttpGet("all-lookups")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>>> GetAll([FromQuery] int? lang = null)
    {
        var result = new Dictionary<string, List<SysCodeLookupDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stockTransactionTypes"] = await GetStockTransactionTypesInternal(lang),
            ["bomTypes"] = await GetLookupsByMgrAsync(SysCodeKeys.BomTypes.Mgr, lang),
            ["costingMethods"] = await GetLookupsByMgrAsync(SysCodeKeys.CostingMethods.Mgr, lang),
            ["itemTypes"] = await GetLookupsByMgrAsync(SysCodeKeys.ItemTypes.Mgr, lang)
        };

        return Ok(ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>.CreateSuccess(result, "Inventory lookups retrieved successfully", 200));
    }

    private async Task<List<SysCodeLookupDto>> GetStockTransactionTypesInternal(int? lang, bool? affectsStockOnly = null)
    {
        // Try getting from DB via TrxType repository first
        try
        {
            var dbTrxTypes = await _trxTypeRepo.GetAllTrxTypesAsync();
            if (dbTrxTypes.Count > 0)
            {
                var isEnglish = lang == 2;
                var filtered = affectsStockOnly == true
                    ? dbTrxTypes.Where(t => t.AffectsStock && t.IsActive)
                    : dbTrxTypes.Where(t => t.IsActive);

                return filtered.Select(t => new SysCodeLookupDto
                {
                    Code = t.TrxCode,
                    Value = t.TrxKey,
                    NameAr = t.TrxNameLocal,
                    NameEn = t.TrxNameEn,
                    Name = isEnglish ? t.TrxNameEn : t.TrxNameLocal,
                    IsActive = t.IsActive ? 1 : 0
                }).OrderBy(x => x.Code).ToList();
            }
        }
        catch
        {
            // Fallback to SysCode table or static
        }

        var list = await GetLookupsByMgrAsync(SysCodeKeys.StockTransactionTypes.Mgr, lang);
        return list;
    }

    /// <summary>
    /// استرجاع أنواع حركات المخزون (Stock Transaction Types)
    /// </summary>
    [HttpGet("stock-transaction-types")]
    [HttpGet("stocktransactiontypes")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetStockTransactionTypes(
        [FromQuery] int? lang = null,
        [FromQuery] bool? affectsStockOnly = null)
    {
        var list = await GetStockTransactionTypesInternal(lang, affectsStockOnly);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Stock transaction types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع هياكل التجميع وقوائم المواد (BOM Types)
    /// </summary>
    [HttpGet("bom-types")]
    [HttpGet("bomtypes")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetBomTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.BomTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "BOM types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع طرق احتساب تكلفة المخزون (Costing Methods)
    /// </summary>
    [HttpGet("costing-methods")]
    [HttpGet("costingmethods")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetCostingMethods([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.CostingMethods.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Costing methods retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع الأصناف في المخزون (Item Types)
    /// </summary>
    [HttpGet("item-types")]
    [HttpGet("itemtypes")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetItemTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ItemTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Item types retrieved successfully", 200));
    }
}
