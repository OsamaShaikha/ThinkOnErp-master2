using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// معايير الفلترة المشتركة لتقارير نقاط البيع
/// </summary>
public class PosReportFilterDto
{
    public long BranchId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? ShiftId { get; set; }
    public long? TillId { get; set; }
    public string? CashierUsername { get; set; }
}

#region 1. تقرير المبيعات العام - Sales Summary
public class PosSalesSummaryReportDto
{
    public int TotalOrders { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalTax { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal DeliveryFees { get; set; }
    public decimal Tips { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalRemainingAmount { get; set; }
    public int RefundOrdersCount { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalGuestsCovers { get; set; }
    public int DineInOrdersCount { get; set; }
    public int TakeawayOrdersCount { get; set; }
    public int DeliveryOrdersCount { get; set; }
    public int OtherOrdersCount { get; set; }
}
#endregion

#region 2. المبيعات حسب طريقة الدفع - Sales by Payment Method
public class PosPaymentMethodReportDto
{
    public int TotalTransactions { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetCollected { get; set; }
    public List<PosPaymentMethodItemDto> Methods { get; set; } = new();
}

public class PosPaymentMethodItemDto
{
    public PosPaymentMethod PaymentMethod { get; set; }
    public int PaymentMethodCode { get; set; }
    public string PaymentMethodNameAr { get; set; } = string.Empty;
    public string PaymentMethodNameEn { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetAmount { get; set; }
    public decimal Percentage { get; set; }
}
#endregion

#region 3. المبيعات حسب الكاشير - Sales by Cashier
public class PosCashierSalesReportDto
{
    public int TotalCashiers { get; set; }
    public int OverallOrdersCount { get; set; }
    public decimal OverallGrossSales { get; set; }
    public decimal OverallDiscounts { get; set; }
    public decimal OverallNetSales { get; set; }
    public List<PosCashierSalesItemDto> Cashiers { get; set; } = new();
}

public class PosCashierSalesItemDto
{
    public string CashierUsername { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalSales { get; set; }
    public int RefundCount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageTicket { get; set; }
}
#endregion

#region 4. مبيعات الأصناف - Sales by Item
public class PosItemSalesReportDto
{
    public int TotalItemsCount { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalGrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalNetSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGrossProfit { get; set; }
    public decimal OverallProfitMargin { get; set; }
    public List<PosItemSalesItemDto> Items { get; set; } = new();
}

public class PosItemSalesItemDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal QuantityRefunded { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal GrossSales { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
}
#endregion

#region 5. المبيعات حسب الفئة - Sales by Category
public class PosCategorySalesReportDto
{
    public int TotalCategoriesCount { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalNetSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGrossProfit { get; set; }
    public List<PosCategorySalesItemDto> Categories { get; set; } = new();
}

public class PosCategorySalesItemDto
{
    public long CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryNameAr { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public int ItemsCount { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal GrossSales { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal PercentageOfTotalSales { get; set; }
}
#endregion

#region 6. الإلغاءات والمرتجعات - Voids and Refunds
public class PosVoidsAndRefundsReportDto
{
    public int TotalVoidCount { get; set; }
    public decimal TotalVoidAmount { get; set; }
    public int TotalRefundCount { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public List<PosVoidLineItemDto> VoidLines { get; set; } = new();
    public List<PosRefundOrderItemDto> RefundOrders { get; set; } = new();
}

public class PosVoidLineItemDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidApprovedBy { get; set; }
    public DateTime VoidedAt { get; set; }
}

public class PosRefundOrderItemDto
{
    public long RefundOrderId { get; set; }
    public string RefundOrderNumber { get; set; } = string.Empty;
    public string? RefundInvoiceNumber { get; set; }
    public long? OriginalOrderId { get; set; }
    public string? OriginalOrderNumber { get; set; }
    public string? OriginalInvoiceNumber { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public string RefundUser { get; set; } = string.Empty;
    public DateTime RefundDate { get; set; }
    public string PaymentMethodName { get; set; } = string.Empty;
}
#endregion

#region 7. الخصومات وتعديل الأسعار - Discounts & Price Adjustments
public class PosDiscountsReportDto
{
    public decimal TotalDiscountAmount { get; set; }
    public int TotalOrdersWithDiscount { get; set; }
    public decimal AverageDiscountPerOrder { get; set; }
    public List<PosDiscountByReasonDto> DiscountsByReason { get; set; } = new();
    public List<PosDiscountByCashierDto> DiscountsByCashier { get; set; } = new();
    public List<PosDiscountDetailItemDto> DiscountDetails { get; set; } = new();
}

public class PosDiscountByReasonDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PosDiscountByCashierDto
{
    public string CashierUsername { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PosDiscountDetailItemDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Scope { get; set; } = string.Empty; // Order or Line
    public string? ItemName { get; set; }
    public string? DiscountReason { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
#endregion

#region 8. فروقات الصندوق - Cash Drawer Variance
public class PosCashDrawerVarianceReportDto
{
    public int TotalShiftsAudited { get; set; }
    public decimal TotalExpectedCash { get; set; }
    public decimal TotalCountedCash { get; set; }
    public decimal TotalShortageAmount { get; set; }
    public decimal TotalOverageAmount { get; set; }
    public decimal NetVarianceAmount { get; set; }
    public decimal NetCardVarianceAmount { get; set; }
    public List<PosShiftVarianceItemDto> Shifts { get; set; } = new();
}

public class PosShiftVarianceItemDto
{
    public long ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public long TillId { get; set; }
    public string? TillName { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCashRefunds { get; set; }
    public decimal TotalFloatIn { get; set; }
    public decimal TotalCashDrop { get; set; }
    public decimal TotalPayOut { get; set; }
    public decimal TotalTipPayout { get; set; }
    public decimal ExpectedCashInDrawer { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public decimal ExpectedCardAmount { get; set; }
    public decimal? CountedCardAmount { get; set; }
    public decimal? CardVarianceAmount { get; set; }
    public bool IsAudited { get; set; }
    public string? AuditedBy { get; set; }
    public string? AuditNotes { get; set; }
}
#endregion

#region 9. الإيداعات والسحوبات النقدية - Cash In / Cash Out
public class PosCashMovementReportDto
{
    public decimal TotalFloatIn { get; set; }
    public decimal TotalCashDrop { get; set; }
    public decimal TotalPayOut { get; set; }
    public decimal TotalTipPayout { get; set; }
    public decimal NetCashMovement { get; set; }
    public List<PosCashMovementDetailDto> Movements { get; set; } = new();
}

public class PosCashMovementDetailDto
{
    public long Id { get; set; }
    public long ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public PosCashMovementType MovementType { get; set; }
    public string MovementTypeNameAr { get; set; } = string.Empty;
    public string MovementTypeNameEn { get; set; } = string.Empty;
    public PosVoucherType VoucherType { get; set; }
    public string VoucherTypeNameAr { get; set; } = string.Empty;
    public string? VoucherNumber { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? BeneficiaryName { get; set; }
    public string? ApprovedBy { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}
#endregion

#region 10. المبيعات حسب الساعة - Hourly Sales
public class PosHourlySalesReportDto
{
    public int PeakHour { get; set; }
    public decimal PeakHourSales { get; set; }
    public string PeakHourFormatted { get; set; } = string.Empty;
    public decimal TotalDaySales { get; set; }
    public int TotalOrders { get; set; }
    public List<PosHourlySlotDto> Hours { get; set; } = new();
}

public class PosHourlySlotDto
{
    public int Hour { get; set; }
    public string HourFormatted { get; set; } = string.Empty; // e.g. "14:00 - 15:00"
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AverageTicket { get; set; }
    public int Covers { get; set; }
    public decimal PercentageOfTotal { get; set; }
}
#endregion

#region 11. المبيعات حسب الطابق والطاولة - Sales by Floor and Table
public class PosFloorTableSalesReportDto
{
    public int TotalTableOrders { get; set; }
    public decimal TotalTableSales { get; set; }
    public int TotalCovers { get; set; }
    public decimal AverageSpendPerCover { get; set; }
    public List<PosTableSalesItemDto> Tables { get; set; } = new();
}

public class PosTableSalesItemDto
{
    public long FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public long TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public int Capacity { get; set; }
    public int OrderCount { get; set; }
    public int CoversCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AverageTicket { get; set; }
    public decimal AveragePerCover { get; set; }
}
#endregion

#region 12. المبيعات حسب نوع الطلب - Sales by Order Type
public class PosOrderTypeSalesReportDto
{
    public int TotalOrdersCount { get; set; }
    public decimal TotalSales { get; set; }
    public List<PosOrderTypeDetailDto> OrderTypes { get; set; } = new();
}

public class PosOrderTypeDetailDto
{
    public PosOrderType OrderType { get; set; }
    public int OrderTypeCode { get; set; }
    public string OrderTypeNameAr { get; set; } = string.Empty;
    public string OrderTypeNameEn { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal Percentage { get; set; }
}
#endregion

#region 13. المبيعات حسب العميل - Sales by Customer
public class PosCustomerSalesReportDto
{
    public int TotalCustomers { get; set; }
    public decimal TotalCustomerSales { get; set; }
    public List<PosCustomerSalesItemDto> Customers { get; set; } = new();
}

public class PosCustomerSalesItemDto
{
    public long? CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AverageTicket { get; set; }
    public DateTime? LastVisitDate { get; set; }
}
#endregion

#region 14. ملخص الضريبة - Tax Summary
public class PosTaxSummaryReportDto
{
    public decimal TotalGrossSales { get; set; }
    public decimal TotalTaxableSales { get; set; }
    public decimal TotalZeroRatedSales { get; set; }
    public decimal TotalExemptSales { get; set; }
    public decimal TotalTaxCollected { get; set; }
    public decimal TotalRefundTax { get; set; }
    public decimal NetTaxPayable { get; set; }
    public List<PosTaxRateBreakdownDto> TaxRatesBreakdown { get; set; } = new();
}

public class PosTaxRateBreakdownDto
{
    public long TaxRateId { get; set; }
    public string TaxRateCode { get; set; } = string.Empty;
    public decimal TaxPercent { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsInclusive { get; set; }
}
#endregion

#region 15. الطاولات المفتوحة مباشر - Open Tables Live
public class PosOpenTablesLiveReportDto
{
    public int TotalTables { get; set; }
    public int AvailableTablesCount { get; set; }
    public int OccupiedTablesCount { get; set; }
    public int BilledTablesCount { get; set; }
    public decimal OccupancyRatePercent { get; set; }
    public decimal ActiveDiningSalesTotal { get; set; }
    public List<PosOpenTableItemDto> OpenTables { get; set; } = new();
}

public class PosOpenTableItemDto
{
    public long TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public long FloorId { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public PosTableStatus Status { get; set; }
    public string StatusNameAr { get; set; } = string.Empty;
    public long? ActiveOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public int Covers { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int DurationMinutes { get; set; }
    public decimal CurrentTotalAmount { get; set; }
    public string? WaiterOrCashier { get; set; }
}
#endregion

#region 16. زمن تحضير المطبخ - Kitchen Prep Time
public class PosKitchenPrepTimeReportDto
{
    public int TotalItemsAnalyzed { get; set; }
    public decimal OverallAveragePrepMinutes { get; set; }
    public int DelayedOrdersThresholdMinutes { get; set; } = 15;
    public int TotalDelayedItemsCount { get; set; }
    public List<PosPrepStationStatDto> Stations { get; set; } = new();
    public List<PosItemPrepStatDto> SlowestItems { get; set; } = new();
}

public class PosPrepStationStatDto
{
    public string PrepStation { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public decimal AveragePrepMinutes { get; set; }
    public decimal MinPrepMinutes { get; set; }
    public decimal MaxPrepMinutes { get; set; }
    public int DelayedCount { get; set; }
}

public class PosItemPrepStatDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string PrepStation { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public decimal AveragePrepMinutes { get; set; }
}
#endregion
