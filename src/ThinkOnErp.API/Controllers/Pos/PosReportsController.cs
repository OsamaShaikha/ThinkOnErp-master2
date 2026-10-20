using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// حزمة تقارير نقاط البيع الشاملة (POS Reports Suite API)
/// تتضمن 16 تقريراً تفصيلياً وتحليلياً للمبيعات، طرق الدفع، الكاشيرات، الأصناف، الفئات، الإلغاءات والمرتجعات،
/// الخصومات، فروقات الصندوق، حركات الدرج، مبيعات الساعة، الطاولات، أنواع الطلبات، العملاء، الضرائب، الطاولات المباشرة، وزمن المطبخ.
/// </summary>
[ApiController]
[Route("api/pos/reports")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosReportsController : ControllerBase
{
    private readonly IPosReportService _reportService;

    public PosReportsController(IPosReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// 1. تقرير المبيعات العام الشامل (Sales Summary / Overview)
    /// </summary>
    [HttpGet("sales-summary")]
    [ProducesResponseType(typeof(ApiResponse<PosSalesSummaryReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosSalesSummaryReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosSalesSummaryReportDto>>> GetSalesSummary(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesSummaryReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 2. تقرير المبيعات حسب طريقة الدفع (Sales by Payment Method)
    /// </summary>
    [HttpGet("sales-by-payment-method")]
    [ProducesResponseType(typeof(ApiResponse<PosPaymentMethodReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosPaymentMethodReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosPaymentMethodReportDto>>> GetSalesByPaymentMethod(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByPaymentMethodReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 3. تقرير المبيعات حسب الكاشير (Sales by Cashier)
    /// </summary>
    [HttpGet("sales-by-cashier")]
    [ProducesResponseType(typeof(ApiResponse<PosCashierSalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCashierSalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCashierSalesReportDto>>> GetSalesByCashier(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByCashierReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 4. تقرير مبيعات الأصناف والربحية (Sales by Item / Product)
    /// </summary>
    [HttpGet("sales-by-item")]
    [ProducesResponseType(typeof(ApiResponse<PosItemSalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosItemSalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosItemSalesReportDto>>> GetSalesByItem(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByItemReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 5. تقرير المبيعات حسب الفئات (Sales by Category)
    /// </summary>
    [HttpGet("sales-by-category")]
    [ProducesResponseType(typeof(ApiResponse<PosCategorySalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCategorySalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCategorySalesReportDto>>> GetSalesByCategory(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByCategoryReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 6. تقرير الإلغاءات والمرتجعات (Voids and Refunds)
    /// </summary>
    [HttpGet("voids-and-refunds")]
    [ProducesResponseType(typeof(ApiResponse<PosVoidsAndRefundsReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosVoidsAndRefundsReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosVoidsAndRefundsReportDto>>> GetVoidsAndRefunds(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetVoidsAndRefundsReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 7. تقرير الخصومات وتعديل الأسعار (Discounts &amp; Price Adjustments)
    /// </summary>
    [HttpGet("discounts")]
    [ProducesResponseType(typeof(ApiResponse<PosDiscountsReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosDiscountsReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosDiscountsReportDto>>> GetDiscounts(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetDiscountsReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 8. تقرير فروقات الصندوق (Cash Drawer Variance)
    /// </summary>
    [HttpGet("drawer-variance")]
    [HttpGet("cash-drawer-variance")]
    [ProducesResponseType(typeof(ApiResponse<PosCashDrawerVarianceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCashDrawerVarianceReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCashDrawerVarianceReportDto>>> GetCashDrawerVariance(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetCashDrawerVarianceReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 9. تقرير الإيداعات والسحوبات النقدية (Cash In / Cash Out Movements)
    /// </summary>
    [HttpGet("cash-movements")]
    [HttpGet("cash-in-out")]
    [ProducesResponseType(typeof(ApiResponse<PosCashMovementReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCashMovementReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCashMovementReportDto>>> GetCashMovements(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetCashMovementsReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 10. تقرير المبيعات حسب الساعة وتحليل أوقات الذروة (Hourly Sales)
    /// </summary>
    [HttpGet("hourly-sales")]
    [ProducesResponseType(typeof(ApiResponse<PosHourlySalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosHourlySalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosHourlySalesReportDto>>> GetHourlySales(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetHourlySalesReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 11. تقرير المبيعات حسب الطابق والطاولة (Sales by Floor and Table)
    /// </summary>
    [HttpGet("sales-by-table")]
    [HttpGet("sales-by-floor-and-table")]
    [ProducesResponseType(typeof(ApiResponse<PosFloorTableSalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosFloorTableSalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosFloorTableSalesReportDto>>> GetSalesByFloorAndTable(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByFloorAndTableReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 12. تقرير المبيعات حسب نوع الطلب (Sales by Order Type: DineIn, Takeaway, Delivery...)
    /// </summary>
    [HttpGet("sales-by-order-type")]
    [ProducesResponseType(typeof(ApiResponse<PosOrderTypeSalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOrderTypeSalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOrderTypeSalesReportDto>>> GetSalesByOrderType(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByOrderTypeReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 13. تقرير المبيعات حسب العميل (Sales by Customer)
    /// </summary>
    [HttpGet("sales-by-customer")]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerSalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCustomerSalesReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosCustomerSalesReportDto>>> GetSalesByCustomer(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetSalesByCustomerReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 14. ملخص الضريبة وضريبة المخرجات (Tax Summary &amp; Output VAT)
    /// </summary>
    [HttpGet("tax-summary")]
    [ProducesResponseType(typeof(ApiResponse<PosTaxSummaryReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosTaxSummaryReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosTaxSummaryReportDto>>> GetTaxSummary(
        [FromQuery] PosReportFilterDto filter,
        CancellationToken ct)
    {
        var result = await _reportService.GetTaxSummaryReportAsync(filter, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 15. تقرير الطاولات المفتوحة المباشر اللحظي (Open Tables Live)
    /// </summary>
    [HttpGet("open-tables")]
    [HttpGet("open-tables-live")]
    [ProducesResponseType(typeof(ApiResponse<PosOpenTablesLiveReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosOpenTablesLiveReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosOpenTablesLiveReportDto>>> GetOpenTablesLive(
        [FromQuery] long branchId,
        [FromQuery] long? floorId,
        CancellationToken ct)
    {
        var result = await _reportService.GetOpenTablesLiveReportAsync(branchId, floorId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 16. تقرير زمن تحضير الطلبات في المطبخ (Kitchen Prep Time via KDS)
    /// </summary>
    [HttpGet("kitchen-prep-time")]
    [ProducesResponseType(typeof(ApiResponse<PosKitchenPrepTimeReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosKitchenPrepTimeReportDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosKitchenPrepTimeReportDto>>> GetKitchenPrepTime(
        [FromQuery] PosReportFilterDto filter,
        [FromQuery] int delayedThresholdMinutes = 15,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetKitchenPrepTimeReportAsync(filter, delayedThresholdMinutes, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
