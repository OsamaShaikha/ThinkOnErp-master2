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
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Payment Methods API: Provides active payment methods filtered specifically for POS cashier stations.
/// </summary>
[ApiController]
[Route("api/pos/payment-methods")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PosPaymentMethodsController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
    }

    /// <summary>
    /// Retrieves payment methods enabled exclusively for Point of Sale (ShowInPos = true, IsActive = true).
    /// </summary>
    /// <param name="branchId">Optional filter by branch.</param>
    /// <param name="methodType">Optional filter by method type (CASH, CARD, BANK, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of POS-ready payment methods.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosPaymentMethodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosPaymentMethodDto>>>> GetPosPaymentMethods(
        [FromQuery] long? branchId = null,
        [FromQuery] string? methodType = null,
        CancellationToken cancellationToken = default)
    {
        // Enforce POS filtering: showInPos = true, activeOnly = true
        var methods = await _paymentMethodService.GetAllAsync(
            branchId: branchId,
            methodType: methodType,
            showInPos: true,
            showInInvoices: null,
            activeOnly: true,
            ct: cancellationToken);

        var posDtos = methods.Select(m => new PosPaymentMethodDto
        {
            Id = m.Id,
            BranchId = m.BranchId,
            Code = m.Code,
            NameLocal = m.NameLocal,
            NameEn = m.NameEn,
            MethodType = m.MethodType,
            CommissionPercent = m.CommissionPercent,
            CommissionFixedAmount = m.CommissionFixedAmount,
            RequiresReference = m.RequiresReference,
            RequiresDueDate = m.RequiresDueDate,
            DisplayOrder = m.DisplayOrder,
            IsActive = m.IsActive
        }).OrderBy(m => m.DisplayOrder).ThenBy(m => m.NameLocal).ToList();

        return Ok(ApiResponse<IReadOnlyList<PosPaymentMethodDto>>.CreateSuccess(
            posDtos,
            "تم جلب وسائل الدفع الخاصة بنقاط البيع بنجاح",
            200));
    }
}
