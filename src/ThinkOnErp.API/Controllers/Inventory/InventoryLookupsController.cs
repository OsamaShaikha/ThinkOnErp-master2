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

    private async Task<List<SysCodeLookupDto>> GetLookupsByMgrAsync(int mgr, int? lang)
    {
        var raw = await _sysCodeRepo.GetActiveByCodeMgrAsync(mgr);
        if (raw.Count > 0)
        {
            return SysCodeLookupHelper.MapToLookupDtos(raw, lang);
        }

        // Fallback static definitions if database table has not been loaded
        return SysCodeLookupHelper.GetFallbackLookups(mgr, lang);
    }

    /// <summary>
    /// استرجاع كافة ثوابت وقوائم المخزون دفعة واحدة لتغذية كاش واجهة المستخدم
    /// </summary>
    [HttpGet("all")]
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
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetItemTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ItemTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Item types retrieved successfully", 200));
    }
}
