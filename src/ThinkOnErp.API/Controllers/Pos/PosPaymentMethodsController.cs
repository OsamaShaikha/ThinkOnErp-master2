using System.Collections.Generic;
using System.Linq;
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
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Payment Methods API: Full CRUD operations for POS payment methods with protection for default system template methods.
/// </summary>
[ApiController]
[Route("api/pos/payment-methods")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPaymentMethodService _paymentMethodService;
    private readonly ILogger<PosPaymentMethodsController> _logger;

    public PosPaymentMethodsController(
        IPaymentMethodRepository paymentMethodRepo,
        IPaymentMethodService paymentMethodService,
        ILogger<PosPaymentMethodsController> logger)
    {
        _paymentMethodRepo = paymentMethodRepo;
        _paymentMethodService = paymentMethodService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves payment methods configured for POS cashiers with system protection indicators.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosPaymentMethodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosPaymentMethodDto>>>> GetPosPaymentMethods(
        [FromQuery] long? branchId = null,
        [FromQuery] int? lang = null,
        [FromQuery] bool? activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var methods = await _paymentMethodRepo.GetAllAsync(
            branchId: branchId,
            methodType: null,
            showInPos: true,
            showInInvoices: null,
            activeOnly: activeOnly,
            ct: cancellationToken);

        var isEnglish = lang == 2;
        var list = new List<PosPaymentMethodDto>();

        foreach (var m in methods)
        {
            list.Add(MapToPosDto(m, isEnglish));
        }

        // ضمان توفر الذمم (99) والضيافة (98) لكل الفروع بشكل دائم
        if (!list.Any(x => x.Id == (int)PaymentMethodConstants.CustomerAccountId || x.Code.Equals(PaymentMethodConstants.CustomerAccount, System.StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(GetCustomerAccountFallback(isEnglish));
        }

        if (!list.Any(x => x.Id == (int)PaymentMethodConstants.HospitalityId || x.Code.Equals(PaymentMethodConstants.Hospitality, System.StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(GetHospitalityFallback(isEnglish));
        }

        // إزالة التكرار وترتيب العرض
        var result = list
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<PosPaymentMethodDto>>.CreateSuccess(
            result,
            "تم جلب وسائل الدفع لنقاط البيع بنجاح مع تثبيت الذمم على 99 والضيافة على 98 لجميع الفروع",
            200));
    }

    /// <summary>
    /// Retrieves a specific POS payment method by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosPaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosPaymentMethodDto>>> GetById(
        long id,
        [FromQuery] int? lang = null,
        CancellationToken cancellationToken = default)
    {
        var isEnglish = lang == 2;

        if (id == PaymentMethodConstants.CustomerAccountId)
            return Ok(ApiResponse<PosPaymentMethodDto>.CreateSuccess(GetCustomerAccountFallback(isEnglish), "تم جلب وسيلة الدفع بنجاح", 200));

        if (id == PaymentMethodConstants.HospitalityId)
            return Ok(ApiResponse<PosPaymentMethodDto>.CreateSuccess(GetHospitalityFallback(isEnglish), "تم جلب وسيلة الدفع بنجاح", 200));

        var method = await _paymentMethodRepo.GetByIdAsync(id, cancellationToken);
        if (method == null)
            return NotFound(ApiResponse<PosPaymentMethodDto>.CreateFailure("وسيلة الدفع غير موجودة", statusCode: 404));

        return Ok(ApiResponse<PosPaymentMethodDto>.CreateSuccess(MapToPosDto(method, isEnglish), "تم جلب وسيلة الدفع بنجاح", 200));
    }

    /// <summary>
    /// Creates a new payment method configured for POS.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PosPaymentMethodDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosPaymentMethodDto>>> Create(
        [FromBody] CreatePaymentMethodDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            return BadRequest(ApiResponse<PosPaymentMethodDto>.CreateFailure("بيانات وسيلة الدفع غير صالحة", statusCode: 400));

        var trimmedCode = dto.Code?.Trim() ?? string.Empty;
        if (PaymentMethodConstants.ProtectedCodes.Contains(trimmedCode))
        {
            return BadRequest(ApiResponse<PosPaymentMethodDto>.CreateFailure(
                $"كود وسيلة الدفع '{trimmedCode}' محجوز لوسائل الدفع الأساسية في النظام",
                errors: new List<string> { "RESERVED_PAYMENT_METHOD_CODE" },
                statusCode: 400));
        }

        // Ensure POS visibility
        dto.ShowInPos = true;

        var username = User.Identity?.Name ?? "system";
        var result = await _paymentMethodService.CreateAsync(dto, username, cancellationToken);

        if (!result.Success || result.Data == null)
            return StatusCode(result.StatusCode, result);

        var posDto = MapFromDto(result.Data);
        return CreatedAtAction(nameof(GetById), new { id = posDto.Id }, ApiResponse<PosPaymentMethodDto>.CreateSuccess(posDto, result.Message, 201));
    }

    /// <summary>
    /// Updates an existing POS payment method.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosPaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosPaymentMethodDto>>> Update(
        long id,
        [FromBody] UpdatePaymentMethodDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            return BadRequest(ApiResponse<PosPaymentMethodDto>.CreateFailure("بيانات التعديل غير صالحة", statusCode: 400));

        var username = User.Identity?.Name ?? "system";
        var result = await _paymentMethodService.UpdateAsync(id, dto, username, cancellationToken);

        if (!result.Success || result.Data == null)
            return StatusCode(result.StatusCode, result);

        var posDto = MapFromDto(result.Data);
        return Ok(ApiResponse<PosPaymentMethodDto>.CreateSuccess(posDto, result.Message, 200));
    }

    /// <summary>
    /// Deletes or deactivates a payment method, with strict protection for default system template methods (DEV_TEMPLATE).
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        long id,
        [FromQuery] bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        // 1. فحص المعرفات المحمية الثابتة (98: ضيافة، 99: ذمم عملاء)
        if (PaymentMethodConstants.ProtectedIds.Contains(id))
        {
            return BadRequest(ApiResponse<bool>.CreateFailure(
                "لا يمكن حذف وسائل الدفع الأساسية المثبتة في قالب النظام الافتراضي (CASH, MADA, VISA, BANK_TRANSFER, CHEQUE, HOSPITALITY, CUSTOMER_ACCOUNT)",
                errors: new List<string> { "CANNOT_DELETE_SYSTEM_PAYMENT_METHOD" },
                statusCode: 400));
        }

        // 2. فحص السجل في قاعدة البيانات
        var existing = await _paymentMethodRepo.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound(ApiResponse<bool>.CreateFailure("وسيلة الدفع غير موجودة", statusCode: 404));
        }

        // 3. التحقق مما إذا كانت وسيلة الدفع من أكواد قالب النظام المحمية
        if (PaymentMethodConstants.IsProtected(existing.Id, existing.Code))
        {
            return BadRequest(ApiResponse<bool>.CreateFailure(
                "لا يمكن حذف وسائل الدفع الأساسية المثبتة في قالب النظام الافتراضي (CASH, MADA, VISA, BANK_TRANSFER, CHEQUE, HOSPITALITY, CUSTOMER_ACCOUNT)",
                errors: new List<string> { "CANNOT_DELETE_SYSTEM_PAYMENT_METHOD" },
                statusCode: 400));
        }

        // 4. الحذف عبر الخدمة
        var response = hardDelete
            ? await _paymentMethodService.HardDeleteAsync(id, cancellationToken)
            : await _paymentMethodService.DeleteAsync(id, cancellationToken);

        if (!response.Success)
            return StatusCode(response.StatusCode, response);

        return Ok(response);
    }

    private static PosPaymentMethodDto MapToPosDto(PaymentMethod m, bool isEnglish = false)
    {
        var codeUpper = m.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var typeUpper = m.MethodType?.Trim().ToUpperInvariant() ?? string.Empty;

        bool isCustAccount = codeUpper == "CUSTOMER_ACCOUNT" || typeUpper is "ACCOUNT" or "CUSTOMER_ACCOUNT" or "CREDIT_ACCOUNT" or "CREDIT";
        bool isHospitality = codeUpper == "HOSPITALITY" || typeUpper == "HOSPITALITY";

        int methodId = isCustAccount ? 99 : (isHospitality ? 98 : (int)m.Id);
        int displayOrder = isCustAccount ? 99 : (isHospitality ? 98 : m.DisplayOrder);
        bool isSystem = PaymentMethodConstants.IsProtected(methodId, m.Code);

        return new PosPaymentMethodDto
        {
            Id = methodId,
            Code = isCustAccount ? "CUSTOMER_ACCOUNT" : (isHospitality ? "HOSPITALITY" : m.Code),
            Name = isEnglish ? (!string.IsNullOrEmpty(m.NameEn) ? m.NameEn : m.NameLocal) : (!string.IsNullOrEmpty(m.NameLocal) ? m.NameLocal : m.NameEn),
            NameEn = m.NameEn,
            Type = isCustAccount ? "ACCOUNT" : (isHospitality ? "HOSPITALITY" : m.MethodType),
            RequiresReference = m.RequiresReference,
            RequiresDueDate = m.RequiresDueDate,
            CommissionPercent = m.CommissionPercent,
            DisplayOrder = displayOrder,
            IsActive = m.IsActive,
            IsSystem = isSystem
        };
    }

    private static PosPaymentMethodDto MapFromDto(PaymentMethodDto dto, bool isEnglish = false)
    {
        var codeUpper = dto.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var typeUpper = dto.MethodType?.Trim().ToUpperInvariant() ?? string.Empty;

        bool isCustAccount = codeUpper == "CUSTOMER_ACCOUNT" || typeUpper is "ACCOUNT" or "CUSTOMER_ACCOUNT" or "CREDIT_ACCOUNT" or "CREDIT";
        bool isHospitality = codeUpper == "HOSPITALITY" || typeUpper == "HOSPITALITY";

        int methodId = isCustAccount ? 99 : (isHospitality ? 98 : (int)dto.Id);
        int displayOrder = isCustAccount ? 99 : (isHospitality ? 98 : dto.DisplayOrder);
        bool isSystem = PaymentMethodConstants.IsProtected(methodId, dto.Code);

        return new PosPaymentMethodDto
        {
            Id = methodId,
            Code = isCustAccount ? "CUSTOMER_ACCOUNT" : (isHospitality ? "HOSPITALITY" : dto.Code),
            Name = isEnglish ? (!string.IsNullOrEmpty(dto.NameEn) ? dto.NameEn : dto.NameLocal) : (!string.IsNullOrEmpty(dto.NameLocal) ? dto.NameLocal : dto.NameEn),
            NameEn = dto.NameEn,
            Type = isCustAccount ? "ACCOUNT" : (isHospitality ? "HOSPITALITY" : dto.MethodType),
            RequiresReference = dto.RequiresReference,
            RequiresDueDate = dto.RequiresDueDate,
            CommissionPercent = dto.CommissionPercent,
            DisplayOrder = displayOrder,
            IsActive = dto.IsActive,
            IsSystem = isSystem
        };
    }

    private static PosPaymentMethodDto GetCustomerAccountFallback(bool isEnglish = false) => new()
    {
        Id = 99,
        Code = "CUSTOMER_ACCOUNT",
        Name = isEnglish ? "Customer Account" : "حساب عميل (آجل)",
        NameEn = "Customer Account",
        Type = "ACCOUNT",
        RequiresReference = false,
        RequiresDueDate = false,
        CommissionPercent = 0,
        DisplayOrder = 99,
        IsActive = true,
        IsSystem = true
    };

    private static PosPaymentMethodDto GetHospitalityFallback(bool isEnglish = false) => new()
    {
        Id = 98,
        Code = "HOSPITALITY",
        Name = isEnglish ? "Hospitality" : "ضيافة",
        NameEn = "Hospitality",
        Type = "HOSPITALITY",
        RequiresReference = false,
        RequiresDueDate = false,
        CommissionPercent = 0,
        DisplayOrder = 98,
        IsActive = true,
        IsSystem = true
    };
}
