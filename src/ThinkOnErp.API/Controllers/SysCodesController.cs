using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// إدارة أكواد النظام والقوائم المنسدلة العامة (System Codes and Lookup Tables API)
/// </summary>
[ApiController]
[Route("api/syscodes")]
[ApiExplorerSettings(GroupName = ApiCategories.System)]
public class SysCodesController : ControllerBase
{
    private readonly ISysCodeRepository _repo;
    private readonly ILogger<SysCodesController> _logger;

    public SysCodesController(ISysCodeRepository repo, ILogger<SysCodesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// استرجاع كافة أكواد النظام في جدول SYS_CODE
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetAll()
    {
        var codes = await _repo.GetAllAsync();
        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Codes retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أرقام فئات الأكواد المتوفرة (Distinct CODE_MGR)
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<List<int>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<int>>>> GetGroups()
    {
        var groups = await _repo.GetDistinctCodeMgrsAsync();
        return Ok(ApiResponse<List<int>>.CreateSuccess(groups, "Code groups retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع وفلترة تعريفات الأكواد (حسب اللغة، الفئة، الكود، أو الوصف)
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetDefinitions(
        [FromQuery] int? codeLang,
        [FromQuery] int? codeMgr,
        [FromQuery] int? codeMnr,
        [FromQuery] string? codeDesc)
    {
        var codes = await _repo.GetDefinitionsAsync(codeLang, codeMgr, codeMnr, codeDesc);
        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Code definitions retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع كافة عناصر فئة معينة لتغذية القوائم المنسدلة في الواجهات (مثل: 32 للألوان، 14 للغات، 16 للأطراف)
    /// </summary>
    [HttpGet("groups/{codeMgr:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetByGroup(
        [FromRoute] int codeMgr,
        [FromQuery] int? codeLang = null,
        [FromQuery] bool activeOnly = true)
    {
        var codes = activeOnly 
            ? await _repo.GetActiveByCodeMgrAsync(codeMgr) 
            : await _repo.GetByCodeMgrAsync(codeMgr);

        if (codeLang.HasValue)
        {
            codes = codes.Where(c => c.CodeLang == codeLang.Value).ToList();
        }

        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();

        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Codes retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع كود محدد بواسطة المفتاح المركب (CODE_MGR, CODE_MNR, CODE_LANG)
    /// </summary>
    [HttpGet("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [ProducesResponseType(typeof(ApiResponse<SysCodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SysCodeDto>>> GetById(int codeMgr, int codeMnr, int codeLang)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<SysCodeDto>.CreateFailure("Code not found", statusCode: 404));

        var dto = new SysCodeDto
        {
            CodeMgr = code.CodeMgr,
            CodeMnr = code.CodeMnr,
            CodeLang = code.CodeLang,
            CodeDesc = code.CodeDesc,
            CodeValue = code.CodeValue,
            IsActive = code.IsActive,
            CreationUser = code.CreationUser,
            CreationDate = code.CreationDate,
            UpdateUser = code.UpdateUser,
            UpdateDate = code.UpdateDate
        };
        return Ok(ApiResponse<SysCodeDto>.CreateSuccess(dto, "Code retrieved successfully", 200));
    }

    #region Dedicated SysCode Lookup Endpoints (GET Only)

    private static readonly Dictionary<string, int> CategoryNameToMgr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["payment-methods"] = SysCodeKeys.PaymentMethods.Mgr,
        ["paymentmethods"] = SysCodeKeys.PaymentMethods.Mgr,
        ["party-types"] = SysCodeKeys.PartyTypes.Mgr,
        ["partytypes"] = SysCodeKeys.PartyTypes.Mgr,
        ["colors"] = SysCodeKeys.ItemColors.Mgr,
        ["item-colors"] = SysCodeKeys.ItemColors.Mgr,
        ["itemcolors"] = SysCodeKeys.ItemColors.Mgr,
        ["document-categories"] = SysCodeKeys.DocumentCategories.Mgr,
        ["documentcategories"] = SysCodeKeys.DocumentCategories.Mgr,
        ["languages"] = SysCodeKeys.Languages.Mgr,
        ["owner-types"] = SysCodeKeys.OwnerTypes.Mgr,
        ["ownertypes"] = SysCodeKeys.OwnerTypes.Mgr,
        ["audit-status"] = SysCodeKeys.AuditStatus.Mgr,
        ["auditstatus"] = SysCodeKeys.AuditStatus.Mgr,
        ["actor-types"] = SysCodeKeys.ActorTypes.Mgr,
        ["actortypes"] = SysCodeKeys.ActorTypes.Mgr,
        ["event-categories"] = SysCodeKeys.EventCategories.Mgr,
        ["eventcategories"] = SysCodeKeys.EventCategories.Mgr,
        ["alert-types"] = SysCodeKeys.AlertTypes.Mgr,
        ["alerttypes"] = SysCodeKeys.AlertTypes.Mgr,
        ["health-status"] = SysCodeKeys.HealthStatus.Mgr,
        ["healthstatus"] = SysCodeKeys.HealthStatus.Mgr,
        ["threat-types"] = SysCodeKeys.ThreatTypes.Mgr,
        ["threattypes"] = SysCodeKeys.ThreatTypes.Mgr,
        ["threat-severity"] = SysCodeKeys.ThreatSeverity.Mgr,
        ["threatseverity"] = SysCodeKeys.ThreatSeverity.Mgr,
        ["audit-severity"] = SysCodeKeys.AuditSeverity.Mgr,
        ["auditseverity"] = SysCodeKeys.AuditSeverity.Mgr,
        ["audit-event-types"] = SysCodeKeys.AuditEventTypes.Mgr,
        ["auditeventtypes"] = SysCodeKeys.AuditEventTypes.Mgr,
        ["payload-logging-levels"] = SysCodeKeys.PayloadLoggingLevels.Mgr,
        ["payloadlogginglevels"] = SysCodeKeys.PayloadLoggingLevels.Mgr,
        ["memory-pressure"] = SysCodeKeys.MemoryPressure.Mgr,
        ["memorypressure"] = SysCodeKeys.MemoryPressure.Mgr,
        ["key-types"] = SysCodeKeys.KeyTypes.Mgr,
        ["keytypes"] = SysCodeKeys.KeyTypes.Mgr,
    };

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
        var raw = await _repo.GetActiveByCodeMgrAsync(mgr);
        return MapToLookupDtos(raw, lang);
    }

    /// <summary>
    /// استرجاع كافة القوائم المنسدلة وأكواد النظام دفعة واحدة لتغذية كاش الواجهات (Consolidated Lookups)
    /// </summary>
    [HttpGet("all-lookups")]
    [HttpGet("lookups")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>>> GetAllLookups([FromQuery] int? lang = null)
    {
        var allCodes = await _repo.GetAllAsync();
        var activeCodes = allCodes.Where(c => c.IsActive == 1 && c.CodeMnr > 0).ToList();

        var result = new Dictionary<string, List<SysCodeLookupDto>>(StringComparer.OrdinalIgnoreCase);

        var keyMapping = new (string Key, int Mgr)[]
        {
            ("paymentMethods", SysCodeKeys.PaymentMethods.Mgr),
            ("partyTypes", SysCodeKeys.PartyTypes.Mgr),
            ("itemColors", SysCodeKeys.ItemColors.Mgr),
            ("documentCategories", SysCodeKeys.DocumentCategories.Mgr),
            ("languages", SysCodeKeys.Languages.Mgr),
            ("ownerTypes", SysCodeKeys.OwnerTypes.Mgr),
            ("auditStatus", SysCodeKeys.AuditStatus.Mgr),
            ("actorTypes", SysCodeKeys.ActorTypes.Mgr),
            ("eventCategories", SysCodeKeys.EventCategories.Mgr),
            ("alertTypes", SysCodeKeys.AlertTypes.Mgr),
            ("healthStatus", SysCodeKeys.HealthStatus.Mgr),
            ("threatTypes", SysCodeKeys.ThreatTypes.Mgr),
            ("threatSeverity", SysCodeKeys.ThreatSeverity.Mgr),
            ("auditSeverity", SysCodeKeys.AuditSeverity.Mgr),
            ("auditEventTypes", SysCodeKeys.AuditEventTypes.Mgr),
            ("payloadLoggingLevels", SysCodeKeys.PayloadLoggingLevels.Mgr),
            ("memoryPressure", SysCodeKeys.MemoryPressure.Mgr),
            ("keyTypes", SysCodeKeys.KeyTypes.Mgr)
        };

        foreach (var (key, mgr) in keyMapping)
        {
            var subset = activeCodes.Where(c => c.CodeMgr == mgr);
            result[key] = MapToLookupDtos(subset, lang);
        }

        return Ok(ApiResponse<Dictionary<string, List<SysCodeLookupDto>>>.CreateSuccess(result, "All system lookups retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع طرق الدفع المعتمدة في النظام (نقدي، آجل، شبكة/بطاقة، تحويل بنكي، شيك)
    /// </summary>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetPaymentMethods([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.PaymentMethods.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Payment methods retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع الأطراف في النظام (عميل، مورد، موظف...)
    /// </summary>
    [HttpGet("party-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetPartyTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.PartyTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Party types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع ألوان الأصناف المعرفة في النظام
    /// </summary>
    [HttpGet("colors")]
    [HttpGet("item-colors")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetColors([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ItemColors.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Colors retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع تصنيفات المستندات (فواتير، عقود، سندات، تقارير...)
    /// </summary>
    [HttpGet("document-categories")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetDocumentCategories([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.DocumentCategories.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Document categories retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع لغات النظام (العربية، الإنجليزية...)
    /// </summary>
    [HttpGet("languages")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetLanguages([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.Languages.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Languages retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع الملاك (شركة، فرع، مشرف عام)
    /// </summary>
    [HttpGet("owner-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetOwnerTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.OwnerTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Owner types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع حالات التدقيق (غير محلول، قيد المعالجة، تم الحل، حرج)
    /// </summary>
    [HttpGet("audit-status")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetAuditStatus([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.AuditStatus.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Audit status list retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع الفاعلين والمستخدمين (مستخدم، مشرف، نظام...)
    /// </summary>
    [HttpGet("actor-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetActorTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ActorTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Actor types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع تصنيفات أحداث التدقيق (تسجيل دخول، تغيير بيانات، أمان...)
    /// </summary>
    [HttpGet("event-categories")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetEventCategories([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.EventCategories.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Event categories retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع التنبيهات في النظام
    /// </summary>
    [HttpGet("alert-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetAlertTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.AlertTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Alert types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع مستويات الحالة الصحية للنظام (سليم، متراجع، غير سليم...)
    /// </summary>
    [HttpGet("health-status")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetHealthStatus([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.HealthStatus.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Health status list retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع التهديدات الأمنية
    /// </summary>
    [HttpGet("threat-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetThreatTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ThreatTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Threat types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع مستويات خطورة التهديدات الأمنية
    /// </summary>
    [HttpGet("threat-severity")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetThreatSeverity([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.ThreatSeverity.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Threat severity list retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع مستويات خطورة التدقيق
    /// </summary>
    [HttpGet("audit-severity")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetAuditSeverity([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.AuditSeverity.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Audit severity list retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع أحداث سجلات التدقيق
    /// </summary>
    [HttpGet("audit-event-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetAuditEventTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.AuditEventTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Audit event types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع مستويات تسجيل البيانات (None, Metadata Only, Full)
    /// </summary>
    [HttpGet("payload-logging-levels")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetPayloadLoggingLevels([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.PayloadLoggingLevels.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Payload logging levels retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع مستويات ضغط الذاكرة
    /// </summary>
    [HttpGet("memory-pressure")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetMemoryPressure([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.MemoryPressure.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Memory pressure levels retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أنواع المفاتيح (API Key, Signing Key, Encryption Key...)
    /// </summary>
    [HttpGet("key-types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetKeyTypes([FromQuery] int? lang = null)
    {
        var list = await GetLookupsByMgrAsync(SysCodeKeys.KeyTypes.Mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Key types retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أي قائمة منسدلة حسب اسم الفئة (مثال: payment-methods, party-types, colors...) أو رقمها
    /// </summary>
    [HttpGet("by-category/{categoryName}")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<SysCodeLookupDto>>>> GetByCategory(string categoryName, [FromQuery] int? lang = null)
    {
        int mgr;
        if (int.TryParse(categoryName, out var parsedMgr))
        {
            mgr = parsedMgr;
        }
        else if (CategoryNameToMgr.TryGetValue(categoryName, out var mappedMgr))
        {
            mgr = mappedMgr;
        }
        else
        {
            return NotFound(ApiResponse<List<SysCodeLookupDto>>.CreateFailure($"Lookup category '{categoryName}' not found", statusCode: 404));
        }

        var list = await GetLookupsByMgrAsync(mgr, lang);
        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(list, "Lookup items retrieved successfully", 200));
    }

    #endregion

    /// <summary>
    /// إضافة كود جديد إلى جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<SysCodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SysCodeDto>>> Create([FromBody] CreateSysCodeDto dto)
    {
        var validationErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CodeDesc))
            validationErrors.Add("Code description is required.");
        else if (dto.CodeDesc.Length > 1000)
            validationErrors.Add("Code description must not exceed 1000 characters.");

        if (string.IsNullOrWhiteSpace(dto.CodeValue))
            validationErrors.Add("Code value is required.");
        else if (dto.CodeValue.Length > 200)
            validationErrors.Add("Code value must not exceed 200 characters.");

        if (dto.IsActive is not (0 or 1))
            validationErrors.Add("IsActive must be either 0 or 1.");

        if (validationErrors.Count > 0)
        {
            return BadRequest(ApiResponse<SysCodeDto>.CreateFailure(
                "One or more validation errors occurred",
                validationErrors,
                400));
        }

        var existing = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(dto.CodeMgr, dto.CodeMnr, dto.CodeLang);
        if (existing != null)
            return BadRequest(ApiResponse<SysCodeDto>.CreateFailure("Code with this key already exists", statusCode: 400));

        var entity = new Domain.Entities.SysCode
        {
            CodeMgr = dto.CodeMgr,
            CodeMnr = dto.CodeMnr,
            CodeLang = dto.CodeLang,
            CodeDesc = dto.CodeDesc,
            CodeValue = dto.CodeValue,
            IsActive = dto.IsActive,
            CreationUser = User.Identity?.Name ?? "system",
            CreationDate = DateTime.UtcNow
        };

        var created = await _repo.AddAsync(entity);
        var result = new SysCodeDto
        {
            CodeMgr = created.CodeMgr,
            CodeMnr = created.CodeMnr,
            CodeLang = created.CodeLang,
            CodeDesc = created.CodeDesc,
            CodeValue = created.CodeValue,
            IsActive = created.IsActive,
            CreationUser = created.CreationUser,
            CreationDate = created.CreationDate
        };

        return CreatedAtAction(nameof(GetById), new { codeMgr = result.CodeMgr, codeMnr = result.CodeMnr, codeLang = result.CodeLang },
            ApiResponse<SysCodeDto>.CreateSuccess(result, "Code created successfully", 201));
    }

    /// <summary>
    /// تعديل بيانات كود محدد في جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpPut("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int codeMgr, int codeMnr, int codeLang, [FromBody] UpdateSysCodeDto dto)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<object>.CreateFailure("Code not found", statusCode: 404));

        code.CodeDesc = dto.CodeDesc;
        code.CodeValue = dto.CodeValue;
        code.IsActive = dto.IsActive;
        code.UpdateUser = User.Identity?.Name ?? "system";
        code.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateAsync(code);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Code updated successfully", 200));
    }

    /// <summary>
    /// حذف كود من جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpDelete("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int codeMgr, int codeMnr, int codeLang)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<object>.CreateFailure("Code not found", statusCode: 404));

        await _repo.DeleteAsync(codeMgr, codeMnr, codeLang);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Code deleted successfully", 200));
    }
}
