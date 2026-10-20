using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Application.Services.Pos;

/// <summary>
/// واجهة خدمة تقارير نقاط البيع (POS Reports Suite)
/// تشمل الـ 16 تقريراً الشاملة للعمليات والمبيعات والتحليلات النقدية والمطبخ.
/// </summary>
public interface IPosReportService
{
    /// <summary>
    /// 1. تقرير المبيعات العام الشامل
    /// </summary>
    Task<ApiResponse<PosSalesSummaryReportDto>> GetSalesSummaryReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 2. تقرير المبيعات حسب طريقة الدفع
    /// </summary>
    Task<ApiResponse<PosPaymentMethodReportDto>> GetSalesByPaymentMethodReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 3. تقرير المبيعات حسب الكاشير
    /// </summary>
    Task<ApiResponse<PosCashierSalesReportDto>> GetSalesByCashierReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 4. تقرير مبيعات الأصناف والربحية
    /// </summary>
    Task<ApiResponse<PosItemSalesReportDto>> GetSalesByItemReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 5. تقرير المبيعات حسب فئات الأصناف
    /// </summary>
    Task<ApiResponse<PosCategorySalesReportDto>> GetSalesByCategoryReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 6. تقرير الإلغاءات والمرتجعات
    /// </summary>
    Task<ApiResponse<PosVoidsAndRefundsReportDto>> GetVoidsAndRefundsReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 7. تقرير الخصومات وتعديل الأسعار
    /// </summary>
    Task<ApiResponse<PosDiscountsReportDto>> GetDiscountsReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 8. تقرير فروقات الصندوق (Cash Drawer Variance)
    /// </summary>
    Task<ApiResponse<PosCashDrawerVarianceReportDto>> GetCashDrawerVarianceReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 9. تقرير الإيداعات والسحوبات النقدية (Cash In / Cash Out)
    /// </summary>
    Task<ApiResponse<PosCashMovementReportDto>> GetCashMovementsReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 10. تقرير المبيعات حسب الساعة (Hourly Sales)
    /// </summary>
    Task<ApiResponse<PosHourlySalesReportDto>> GetHourlySalesReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 11. تقرير المبيعات حسب الطابق والطاولة (Sales by Floor &amp; Table)
    /// </summary>
    Task<ApiResponse<PosFloorTableSalesReportDto>> GetSalesByFloorAndTableReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 12. تقرير المبيعات حسب نوع الطلب (Order Type - DineIn, Takeaway, Delivery...)
    /// </summary>
    Task<ApiResponse<PosOrderTypeSalesReportDto>> GetSalesByOrderTypeReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 13. تقرير المبيعات حسب العميل (Sales by Customer)
    /// </summary>
    Task<ApiResponse<PosCustomerSalesReportDto>> GetSalesByCustomerReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 14. ملخص الضريبة (Tax Summary &amp; Output VAT)
    /// </summary>
    Task<ApiResponse<PosTaxSummaryReportDto>> GetTaxSummaryReportAsync(PosReportFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 15. تقرير الطاولات المفتوحة المباشر اللحظي (Open Tables Live)
    /// </summary>
    Task<ApiResponse<PosOpenTablesLiveReportDto>> GetOpenTablesLiveReportAsync(long branchId, long? floorId = null, CancellationToken ct = default);

    /// <summary>
    /// 16. تقرير زمن تحضير الطلبات في المطبخ (Kitchen Prep Time via KDS)
    /// </summary>
    Task<ApiResponse<PosKitchenPrepTimeReportDto>> GetKitchenPrepTimeReportAsync(PosReportFilterDto filter, int delayedThresholdMinutes = 15, CancellationToken ct = default);
}
