using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Payment Methods API: Provides active payment methods directly from the enterprise Payment Methods accounting ledger.
/// </summary>
[ApiController]
[Route("api/pos/payment-methods")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodRepository _paymentMethodRepo;

    public PosPaymentMethodsController(IPaymentMethodRepository paymentMethodRepo)
    {
        _paymentMethodRepo = paymentMethodRepo;
    }

    /// <summary>
    /// Retrieves active payment methods configured for POS cashiers with their exact numeric IDs, accounting GL accounts, and fees.
    /// </summary>
    /// <param name="branchId">Optional branch filter.</param>
    /// <param name="lang">Optional language filter (1 = Arabic, 2 = English).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of POS payment methods configured in the accounting ledger.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosPaymentMethodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosPaymentMethodDto>>>> GetPosPaymentMethods(
        [FromQuery] long? branchId = null,
        [FromQuery] int? lang = null,
        CancellationToken cancellationToken = default)
    {
        var methods = await _paymentMethodRepo.GetAllAsync(
            branchId: branchId,
            methodType: null,
            showInPos: true,
            showInInvoices: null,
            activeOnly: true,
            ct: cancellationToken);

        var isEnglish = lang == 2;
        var list = new List<PosPaymentMethodDto>();

        foreach (var m in methods)
        {
            var codeUpper = m.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            var typeUpper = m.MethodType?.Trim().ToUpperInvariant() ?? string.Empty;

            bool isCustAccount = codeUpper == "CUSTOMER_ACCOUNT" || typeUpper is "ACCOUNT" or "CUSTOMER_ACCOUNT" or "CREDIT_ACCOUNT" or "CREDIT";
            bool isHospitality = codeUpper == "HOSPITALITY" || typeUpper == "HOSPITALITY";

            // تثبيت الذمم على 99 والضيافة على 98 لجميع الفروع
            int methodId = isCustAccount ? 99 : (isHospitality ? 98 : (int)m.Id);
            int displayOrder = isCustAccount ? 99 : (isHospitality ? 98 : m.DisplayOrder);

            list.Add(new PosPaymentMethodDto
            {
                Id = methodId,
                Code = isCustAccount ? "CUSTOMER_ACCOUNT" : (isHospitality ? "HOSPITALITY" : m.Code),
                Name = isEnglish ? (!string.IsNullOrEmpty(m.NameEn) ? m.NameEn : m.NameLocal) : (!string.IsNullOrEmpty(m.NameLocal) ? m.NameLocal : m.NameEn),
                NameEn = m.NameEn,
                Type = isCustAccount ? "ACCOUNT" : (isHospitality ? "HOSPITALITY" : m.MethodType),
                RequiresReference = m.RequiresReference,
                RequiresDueDate = m.RequiresDueDate,
                GlAccountCode = isHospitality ? "632102" : (isCustAccount ? "112101" : m.GlAccountCode),
                CommissionPercent = m.CommissionPercent,
                DisplayOrder = displayOrder,
                IsActive = m.IsActive
            });
        }

        // ضمان توفر الذمم (99) والضيافة (98) لكل الفروع بشكل دائم
        if (!list.Any(x => x.Id == 99 || x.Code == "CUSTOMER_ACCOUNT"))
        {
            list.Add(new PosPaymentMethodDto
            {
                Id = 99,
                Code = "CUSTOMER_ACCOUNT",
                Name = isEnglish ? "Customer Account" : "حساب عميل (آجل)",
                NameEn = "Customer Account",
                Type = "ACCOUNT",
                RequiresReference = false,
                RequiresDueDate = false,
                GlAccountCode = "112101",
                CommissionPercent = 0,
                DisplayOrder = 99,
                IsActive = true
            });
        }

        if (!list.Any(x => x.Id == 98 || x.Code == "HOSPITALITY"))
        {
            list.Add(new PosPaymentMethodDto
            {
                Id = 98,
                Code = "HOSPITALITY",
                Name = isEnglish ? "Hospitality" : "ضيافة",
                NameEn = "Hospitality",
                Type = "HOSPITALITY",
                RequiresReference = false,
                RequiresDueDate = false,
                GlAccountCode = "632102",
                CommissionPercent = 0,
                DisplayOrder = 98,
                IsActive = true
            });
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
}
