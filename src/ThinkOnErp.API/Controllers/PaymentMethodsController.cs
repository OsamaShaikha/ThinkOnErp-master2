using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.PaymentMethods;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// إدارة وسائل وطرق الدفع المالي ومحدداتها المحاسبية (Payment Methods API)
/// </summary>
[ApiController]
[Route("api/accounting/payment-methods")]
[Route("api/payment-methods")]
[ApiExplorerSettings(GroupName = ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _service;
    private readonly ILogger<PaymentMethodsController> _logger;

    public PaymentMethodsController(IPaymentMethodService service, ILogger<PaymentMethodsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// استرجاع قائمة طرق ووسائل الدفع مع الفلترة حسب الفرع، النوع، ومكان الظهور (POS / فواتير / سندات)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentMethodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentMethodDto>>>> GetAll(
        [FromQuery] long? branchId,
        [FromQuery] string? methodType,
        [FromQuery] bool? showInPos,
        [FromQuery] bool? showInInvoices,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var list = await _service.GetAllAsync(branchId, methodType, showInPos, showInInvoices, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PaymentMethodDto>>.CreateSuccess(list, "تم جلب وسائل الدفع بنجاح", 200));
    }

    /// <summary>
    /// استرجاع تفاصيل وسيلة دفع محددة بواسطة المعرف الرقمي (ID)
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        if (item == null)
            return NotFound(ApiResponse<PaymentMethodDto>.CreateFailure("وسيلة الدفع غير موجودة", statusCode: 404));

        return Ok(ApiResponse<PaymentMethodDto>.CreateSuccess(item, "تم جلب وسيلة الدفع بنجاح", 200));
    }

    /// <summary>
    /// استرجاع وسيلة دفع بواسطة الكود (Code) ورقم الفرع
    /// </summary>
    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> GetByCode(
        string code,
        [FromQuery] long branchId = 1,
        CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByCodeAsync(branchId, code, cancellationToken);
        if (item == null)
            return NotFound(ApiResponse<PaymentMethodDto>.CreateFailure($"وسيلة الدفع بالكود '{code}' غير موجودة", statusCode: 404));

        return Ok(ApiResponse<PaymentMethodDto>.CreateSuccess(item, "تم جلب وسيلة الدفع بنجاح", 200));
    }

    /// <summary>
    /// إنشاء وسيلة دفع جديدة مع ربطها المحاسبي الكامل
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> Create(
        [FromBody] CreatePaymentMethodDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var response = await _service.CreateAsync(dto, username, cancellationToken);

        if (!response.Success)
            return BadRequest(response);

        return CreatedAtAction(nameof(GetById), new { id = response.Data?.Id }, response);
    }

    /// <summary>
    /// تعديل بيانات وسيلة دفع والروابط المحاسبية والعمولات
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> Update(
        long id,
        [FromBody] UpdatePaymentMethodDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var response = await _service.UpdateAsync(id, dto, username, cancellationToken);

        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }

    /// <summary>
    /// تعطيل (Soft Delete) وسيلة دفع
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var response = await _service.DeleteAsync(id, cancellationToken);
        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }

    /// <summary>
    /// تهيئة طرق الدفع القياسية لفرع محدد (نقدي، مدى، فيزا، تحويل بنكي، شيك)
    /// </summary>
    [HttpPost("seed-defaults/{branchId:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> SeedDefaults(
        long branchId,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var response = await _service.SeedDefaultPaymentMethodsAsync(branchId, username, cancellationToken);
        return Ok(response);
    }
}
