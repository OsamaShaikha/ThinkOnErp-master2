using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Types;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Document Types and Transaction Types API: Manages all document definitions and transaction types.
/// </summary>
[ApiController]
[Route("api/inventory/documents")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class TrxTypesController : ControllerBase
{
    private readonly ITrxTypeService _service;

    public TrxTypesController(ITrxTypeService service)
    {
        _service = service;
    }

    #region Document Types Endpoints

    /// <summary>
    /// استرجاع كافة أنواع المستندات (Sales Invoices, Purchase Bills, Transfers, etc.)
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<TrxDocTypeDto>>), 200)]
    public async Task<IActionResult> GetDocTypes(CancellationToken ct)
    {
        var result = await _service.GetAllDocTypesAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع تفاصيل نوع مستند محدد بواسطة الكود (100, 200, 300, etc.)
    /// </summary>
    [HttpGet("types/{typeCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 404)]
    public async Task<IActionResult> GetDocTypeByCode([FromRoute] int typeCode, CancellationToken ct)
    {
        var result = await _service.GetDocTypeByCodeAsync(typeCode, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// إنشاء نوع مستند تجاري/مخزني جديد
    /// </summary>
    [HttpPost("types")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 400)]
    public async Task<IActionResult> CreateDocType([FromBody] CreateTrxDocTypeDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.CreateDocTypeAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات نوع مستند
    /// </summary>
    [HttpPut("types/{typeCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocTypeDto>), 404)]
    public async Task<IActionResult> UpdateDocType([FromRoute] int typeCode, [FromBody] UpdateTrxDocTypeDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.UpdateDocTypeAsync(typeCode, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف نوع مستند (غير مسموح بحذف الأنواع الأساسية المحجوزة للنظام)
    /// </summary>
    [HttpDelete("types/{typeCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    [ProducesResponseType(typeof(ApiResponse<bool>), 400)]
    [ProducesResponseType(typeof(ApiResponse<bool>), 404)]
    public async Task<IActionResult> DeleteDocType([FromRoute] int typeCode, CancellationToken ct)
    {
        var result = await _service.DeleteDocTypeAsync(typeCode, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion

    #region Transaction Types Endpoints

    /// <summary>
    /// استرجاع كافة أنواع الحركات التجارية والمخزنية
    /// </summary>
    [HttpGet("transaction-types")]
    [HttpGet("trx-types")]
    [ProducesResponseType(typeof(ApiResponse<List<TrxTransactionTypeDto>>), 200)]
    public async Task<IActionResult> GetAllTrxTypes(CancellationToken ct)
    {
        var result = await _service.GetAllTrxTypesAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع أنواع الحركات التابعة لنوع مستند معين (لتغذية القوائم المنسدلة في شاشات الفواتير)
    /// </summary>
    [HttpGet("types/{docTypeCode:int}/transaction-types")]
    [ProducesResponseType(typeof(ApiResponse<List<TrxTransactionTypeDto>>), 200)]
    public async Task<IActionResult> GetTrxTypesByDocType([FromRoute] int docTypeCode, CancellationToken ct)
    {
        var result = await _service.GetTrxTypesByDocTypeAsync(docTypeCode, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع تفاصيل نوع حركة محددة بواسطة الكود (1001, 1002, 2001, etc.)
    /// </summary>
    [HttpGet("transaction-types/{trxCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 404)]
    public async Task<IActionResult> GetTrxTypeByCode([FromRoute] int trxCode, CancellationToken ct)
    {
        var result = await _service.GetTrxTypeByCodeAsync(trxCode, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// إنشاء نوع حركة تجارية/مخزنية جديدة وربطها بنوع المستند وقواعد التوجيه المحاسبي
    /// </summary>
    [HttpPost("transaction-types")]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 400)]
    public async Task<IActionResult> CreateTrxType([FromBody] CreateTrxTransactionTypeDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.CreateTrxTypeAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات نوع الحركة وقواعد التأثير المخزني والمالي
    /// </summary>
    [HttpPut("transaction-types/{trxCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<TrxTransactionTypeDto>), 404)]
    public async Task<IActionResult> UpdateTrxType([FromRoute] int trxCode, [FromBody] UpdateTrxTransactionTypeDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.UpdateTrxTypeAsync(trxCode, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف نوع حركة
    /// </summary>
    [HttpDelete("transaction-types/{trxCode:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    [ProducesResponseType(typeof(ApiResponse<bool>), 404)]
    public async Task<IActionResult> DeleteTrxType([FromRoute] int trxCode, CancellationToken ct)
    {
        var result = await _service.DeleteTrxTypeAsync(trxCode, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion
}
