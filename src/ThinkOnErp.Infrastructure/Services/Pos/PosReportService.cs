using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Pos;

/// <summary>
/// تنفيذ خدمة تقارير نقاط البيع (POS Reports Suite Implementation)
/// يوفر استعلامات عالية الأداء وتقارير تحليلية شاملة للعمليات والمبيعات والصندوق والمطبخ.
/// </summary>
public class PosReportService : IPosReportService
{
    private readonly OracleDbContext _context;
    private readonly ILogger<PosReportService> _logger;

    public PosReportService(OracleDbContext context, ILogger<PosReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static (DateTime Start, DateTime End) NormalizeDateRange(DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        if (endDate.HasValue && end.TimeOfDay == TimeSpan.Zero)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        return (start, end);
    }

    #region 1. تقرير المبيعات العام - Sales Summary
    public async Task<ApiResponse<PosSalesSummaryReportDto>> GetSalesSummaryReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Where(o => o.BranchId == filter.BranchId && o.CreationDate >= start && o.CreationDate <= end && o.IsActive);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);

            var nonRefundOrders = orders.Where(o => o.Status != PosOrderStatus.Voided && !o.IsRefund).ToList();
            var refundOrders = orders.Where(o => o.Status != PosOrderStatus.Voided && o.IsRefund).ToList();

            var grossSales = nonRefundOrders.Sum(o => o.SubtotalAmount);
            var totalDiscount = nonRefundOrders.Sum(o => o.DiscountAmount);
            var netSales = grossSales - totalDiscount;
            var totalTax = nonRefundOrders.Sum(o => o.TaxAmount);
            var serviceCharge = nonRefundOrders.Sum(o => o.ServiceChargeAmount);
            var deliveryFees = nonRefundOrders.Sum(o => o.DeliveryFee);
            var tips = nonRefundOrders.Sum(o => o.TipAmount);
            var totalSales = nonRefundOrders.Sum(o => o.TotalAmount);
            var totalPaid = nonRefundOrders.Sum(o => o.PaidAmount);
            var totalRefunds = refundOrders.Sum(o => Math.Abs(o.TotalAmount));
            var netRevenue = totalSales - totalRefunds;

            var result = new PosSalesSummaryReportDto
            {
                TotalOrders = nonRefundOrders.Count,
                GrossSales = grossSales,
                TotalDiscount = totalDiscount,
                NetSales = netSales,
                TotalTax = totalTax,
                ServiceCharge = serviceCharge,
                DeliveryFees = deliveryFees,
                Tips = tips,
                TotalSales = totalSales,
                TotalPaidAmount = totalPaid,
                TotalRemainingAmount = Math.Max(0, totalSales - totalPaid),
                RefundOrdersCount = refundOrders.Count,
                TotalRefundAmount = totalRefunds,
                NetRevenue = netRevenue,
                AverageOrderValue = nonRefundOrders.Count > 0 ? Math.Round(netRevenue / nonRefundOrders.Count, 2) : 0m,
                TotalGuestsCovers = nonRefundOrders.Sum(o => o.Covers),
                DineInOrdersCount = nonRefundOrders.Count(o => o.OrderType == PosOrderType.DineIn),
                TakeawayOrdersCount = nonRefundOrders.Count(o => o.OrderType == PosOrderType.Takeaway),
                DeliveryOrdersCount = nonRefundOrders.Count(o => o.OrderType == PosOrderType.Delivery),
                OtherOrdersCount = nonRefundOrders.Count(o => o.OrderType != PosOrderType.DineIn && o.OrderType != PosOrderType.Takeaway && o.OrderType != PosOrderType.Delivery)
            };

            return ApiResponse<PosSalesSummaryReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات العام بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات العام للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosSalesSummaryReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير المبيعات العام: " + ex.Message);
        }
    }
    #endregion

    #region 2. المبيعات حسب طريقة الدفع - Sales by Payment Method
    public async Task<ApiResponse<PosPaymentMethodReportDto>> GetSalesByPaymentMethodReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderPayments
                .AsNoTracking()
                .Include(p => p.Order)
                .Where(p => p.Order != null &&
                            p.Order.BranchId == filter.BranchId &&
                            p.Order.CreationDate >= start &&
                            p.Order.CreationDate <= end &&
                            p.Order.IsActive &&
                            p.Order.Status != PosOrderStatus.Voided);

            if (filter.ShiftId.HasValue)
                query = query.Where(p => p.Order!.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(p => p.Order!.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(p => p.Order!.CreationUser == filter.CashierUsername);

            var payments = await query.ToListAsync(ct);

            var grouped = payments
                .GroupBy(p => p.PaymentMethod)
                .Select(g =>
                {
                    var nonRefundPayments = g.Where(p => !p.Order!.IsRefund).ToList();
                    var refundPayments = g.Where(p => p.Order!.IsRefund).ToList();

                    var collected = nonRefundPayments.Sum(p => p.Amount);
                    var refunded = refundPayments.Sum(p => Math.Abs(p.Amount));
                    var net = collected - refunded;
                    var (ar, en) = GetPaymentMethodNames(g.Key);

                    return new PosPaymentMethodItemDto
                    {
                        PaymentMethod = g.Key,
                        PaymentMethodCode = (int)g.Key,
                        PaymentMethodNameAr = ar,
                        PaymentMethodNameEn = en,
                        TransactionCount = nonRefundPayments.Count,
                        TotalAmount = collected,
                        TotalRefunded = refunded,
                        NetAmount = net
                    };
                })
                .ToList();

            var totalCollected = grouped.Sum(x => x.TotalAmount);
            var totalRefunded = grouped.Sum(x => x.TotalRefunded);
            var netCollected = totalCollected - totalRefunded;

            foreach (var item in grouped)
            {
                item.Percentage = totalCollected > 0 ? Math.Round((item.TotalAmount / totalCollected) * 100m, 2) : 0m;
            }

            var result = new PosPaymentMethodReportDto
            {
                TotalTransactions = grouped.Sum(x => x.TransactionCount),
                TotalCollected = totalCollected,
                TotalRefunded = totalRefunded,
                NetCollected = netCollected,
                Methods = grouped.OrderByDescending(x => x.NetAmount).ToList()
            };

            return ApiResponse<PosPaymentMethodReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب طرق الدفع بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات حسب طريقة الدفع للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosPaymentMethodReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير طرق الدفع: " + ex.Message);
        }
    }
    #endregion

    #region 3. المبيعات حسب الكاشير - Sales by Cashier
    public async Task<ApiResponse<PosCashierSalesReportDto>> GetSalesByCashierReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);

            var grouped = orders
                .GroupBy(o => string.IsNullOrWhiteSpace(o.CreationUser) ? "SYSTEM" : o.CreationUser)
                .Select(g =>
                {
                    var nonRefund = g.Where(o => !o.IsRefund).ToList();
                    var refund = g.Where(o => o.IsRefund).ToList();

                    var gross = nonRefund.Sum(o => o.SubtotalAmount);
                    var discount = nonRefund.Sum(o => o.DiscountAmount);
                    var tax = nonRefund.Sum(o => o.TaxAmount);
                    var total = nonRefund.Sum(o => o.TotalAmount);
                    var refundAmount = refund.Sum(o => Math.Abs(o.TotalAmount));
                    var net = total - refundAmount;

                    return new PosCashierSalesItemDto
                    {
                        CashierUsername = g.Key,
                        OrderCount = nonRefund.Count,
                        GrossSales = gross,
                        TotalDiscount = discount,
                        TotalTax = tax,
                        TotalSales = total,
                        RefundCount = refund.Count,
                        RefundAmount = refundAmount,
                        NetSales = net,
                        AverageTicket = nonRefund.Count > 0 ? Math.Round(net / nonRefund.Count, 2) : 0m
                    };
                })
                .OrderByDescending(c => c.NetSales)
                .ToList();

            var result = new PosCashierSalesReportDto
            {
                TotalCashiers = grouped.Count,
                OverallOrdersCount = grouped.Sum(x => x.OrderCount),
                OverallGrossSales = grouped.Sum(x => x.GrossSales),
                OverallDiscounts = grouped.Sum(x => x.TotalDiscount),
                OverallNetSales = grouped.Sum(x => x.NetSales),
                Cashiers = grouped
            };

            return ApiResponse<PosCashierSalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب الكاشير بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات حسب الكاشير للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosCashierSalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات الكاشير: " + ex.Message);
        }
    }
    #endregion

    #region 4. مبيعات الأصناف - Sales by Item
    public async Task<ApiResponse<PosItemSalesReportDto>> GetSalesByItemReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderLines
                .AsNoTracking()
                .Include(l => l.Order)
                .Include(l => l.Item)
                    .ThenInclude(i => i!.Category)
                .Where(l => l.Order != null &&
                            l.Order.BranchId == filter.BranchId &&
                            l.Order.CreationDate >= start &&
                            l.Order.CreationDate <= end &&
                            l.Order.IsActive &&
                            l.Order.Status != PosOrderStatus.Voided &&
                            !l.IsVoided);

            if (filter.ShiftId.HasValue)
                query = query.Where(l => l.Order!.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(l => l.Order!.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(l => l.Order!.CreationUser == filter.CashierUsername);

            var lines = await query.ToListAsync(ct);

            var grouped = lines
                .GroupBy(l => new { l.ItemId, l.ItemCode, l.ItemName })
                .Select(g =>
                {
                    var nonRefundLines = g.Where(l => !l.Order!.IsRefund).ToList();
                    var refundLines = g.Where(l => l.Order!.IsRefund).ToList();

                    var qtySold = nonRefundLines.Sum(l => l.Quantity);
                    var qtyRefunded = refundLines.Sum(l => Math.Abs(l.Quantity));
                    var netQty = qtySold - qtyRefunded;

                    var grossSales = nonRefundLines.Sum(l => l.Quantity * l.UnitPrice);
                    var discountAmount = nonRefundLines.Sum(l => l.DiscountAmount);
                    var taxAmount = nonRefundLines.Sum(l => l.TaxAmount);
                    var netSales = nonRefundLines.Sum(l => l.LineTotal) - refundLines.Sum(l => Math.Abs(l.LineTotal));
                    var totalCost = nonRefundLines.Sum(l => l.Quantity * l.CostPrice) - refundLines.Sum(l => Math.Abs(l.Quantity * l.CostPrice));
                    var grossProfit = netSales - totalCost;
                    var margin = netSales > 0 ? Math.Round((grossProfit / netSales) * 100m, 2) : 0m;

                    var firstItem = g.FirstOrDefault()?.Item;
                    var catId = firstItem?.CategoryId;
                    var catName = firstItem?.Category?.CategoryNameLocal ?? firstItem?.Category?.CategoryNameEn;

                    return new PosItemSalesItemDto
                    {
                        ItemId = g.Key.ItemId,
                        ItemCode = g.Key.ItemCode,
                        ItemName = g.Key.ItemName,
                        CategoryId = catId,
                        CategoryName = catName,
                        QuantitySold = qtySold,
                        QuantityRefunded = qtyRefunded,
                        NetQuantity = netQty,
                        GrossSales = grossSales,
                        DiscountAmount = discountAmount,
                        TaxAmount = taxAmount,
                        NetSales = netSales,
                        TotalCost = totalCost,
                        GrossProfit = grossProfit,
                        ProfitMarginPercentage = margin
                    };
                })
                .OrderByDescending(i => i.NetSales)
                .ToList();

            var totalNetSales = grouped.Sum(x => x.NetSales);
            var totalCost = grouped.Sum(x => x.TotalCost);
            var totalProfit = totalNetSales - totalCost;

            var result = new PosItemSalesReportDto
            {
                TotalItemsCount = grouped.Count,
                TotalQuantitySold = grouped.Sum(x => x.NetQuantity),
                TotalGrossSales = grouped.Sum(x => x.GrossSales),
                TotalDiscounts = grouped.Sum(x => x.DiscountAmount),
                TotalNetSales = totalNetSales,
                TotalCost = totalCost,
                TotalGrossProfit = totalProfit,
                OverallProfitMargin = totalNetSales > 0 ? Math.Round((totalProfit / totalNetSales) * 100m, 2) : 0m,
                Items = grouped
            };

            return ApiResponse<PosItemSalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير مبيعات الأصناف بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير مبيعات الأصناف للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosItemSalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات الأصناف: " + ex.Message);
        }
    }
    #endregion

    #region 5. المبيعات حسب الفئة - Sales by Category
    public async Task<ApiResponse<PosCategorySalesReportDto>> GetSalesByCategoryReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderLines
                .AsNoTracking()
                .Include(l => l.Order)
                .Include(l => l.Item)
                    .ThenInclude(i => i!.Category)
                .Where(l => l.Order != null &&
                            l.Order.BranchId == filter.BranchId &&
                            l.Order.CreationDate >= start &&
                            l.Order.CreationDate <= end &&
                            l.Order.IsActive &&
                            l.Order.Status != PosOrderStatus.Voided &&
                            !l.IsVoided);

            if (filter.ShiftId.HasValue)
                query = query.Where(l => l.Order!.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(l => l.Order!.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(l => l.Order!.CreationUser == filter.CashierUsername);

            var lines = await query.ToListAsync(ct);

            var grouped = lines
                .GroupBy(l => l.Item?.Category != null
                    ? l.Item.Category.Id
                    : (l.Item?.CategoryId ?? 0))
                .Select(g =>
                {
                    var cat = g.Select(x => x.Item?.Category).FirstOrDefault(c => c != null);
                    var catId = g.Key;
                    var catCode = cat?.CategoryCode.ToString() ?? "0";
                    var catNameAr = cat?.CategoryNameLocal ?? "بدون فئة / عام";
                    var catNameEn = cat?.CategoryNameEn ?? "Uncategorized";

                    var nonRefund = g.Where(l => !l.Order!.IsRefund).ToList();
                    var refund = g.Where(l => l.Order!.IsRefund).ToList();

                    var qtySold = nonRefund.Sum(l => l.Quantity) - refund.Sum(l => Math.Abs(l.Quantity));
                    var gross = nonRefund.Sum(l => l.Quantity * l.UnitPrice);
                    var discount = nonRefund.Sum(l => l.DiscountAmount);
                    var tax = nonRefund.Sum(l => l.TaxAmount);
                    var netSales = nonRefund.Sum(l => l.LineTotal) - refund.Sum(l => Math.Abs(l.LineTotal));
                    var cost = nonRefund.Sum(l => l.Quantity * l.CostPrice) - refund.Sum(l => Math.Abs(l.Quantity * l.CostPrice));
                    var profit = netSales - cost;
                    var distinctItems = g.Select(l => l.ItemId).Distinct().Count();

                    return new PosCategorySalesItemDto
                    {
                        CategoryId = catId,
                        CategoryCode = catCode,
                        CategoryNameAr = catNameAr,
                        CategoryNameEn = catNameEn,
                        ItemsCount = distinctItems,
                        QuantitySold = qtySold,
                        GrossSales = gross,
                        DiscountAmount = discount,
                        TaxAmount = tax,
                        NetSales = netSales,
                        TotalCost = cost,
                        GrossProfit = profit
                    };
                })
                .OrderByDescending(c => c.NetSales)
                .ToList();

            var totalNetSales = grouped.Sum(x => x.NetSales);
            foreach (var item in grouped)
            {
                item.PercentageOfTotalSales = totalNetSales > 0 ? Math.Round((item.NetSales / totalNetSales) * 100m, 2) : 0m;
            }

            var result = new PosCategorySalesReportDto
            {
                TotalCategoriesCount = grouped.Count,
                TotalQuantitySold = grouped.Sum(x => x.QuantitySold),
                TotalNetSales = totalNetSales,
                TotalCost = grouped.Sum(x => x.TotalCost),
                TotalGrossProfit = grouped.Sum(x => x.GrossProfit),
                Categories = grouped
            };

            return ApiResponse<PosCategorySalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب الفئات بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات حسب الفئة للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosCategorySalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات الفئات: " + ex.Message);
        }
    }
    #endregion

    #region 6. الإلغاءات والمرتجعات - Voids and Refunds
    public async Task<ApiResponse<PosVoidsAndRefundsReportDto>> GetVoidsAndRefundsReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            // 1. Voids
            var voidLinesQuery = _context.PosOrderLines
                .AsNoTracking()
                .Include(l => l.Order)
                .Where(l => l.Order != null &&
                            l.Order.BranchId == filter.BranchId &&
                            l.Order.CreationDate >= start &&
                            l.Order.CreationDate <= end &&
                            l.IsVoided);

            if (filter.ShiftId.HasValue)
                voidLinesQuery = voidLinesQuery.Where(l => l.Order!.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                voidLinesQuery = voidLinesQuery.Where(l => l.Order!.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                voidLinesQuery = voidLinesQuery.Where(l => l.Order!.CreationUser == filter.CashierUsername);

            var voidLines = await voidLinesQuery
                .OrderByDescending(l => l.Order!.CreationDate)
                .Select(l => new PosVoidLineItemDto
                {
                    OrderId = l.OrderId,
                    OrderNumber = l.Order!.OrderNumber,
                    LineNumber = l.LineNumber,
                    ItemId = l.ItemId,
                    ItemCode = l.ItemCode,
                    ItemName = l.ItemName,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    VoidReason = l.VoidReason,
                    VoidApprovedBy = l.VoidApprovedBy,
                    VoidedAt = l.Order.CreationDate
                })
                .ToListAsync(ct);

            // 2. Refunds
            var refundsQuery = _context.PosOrderHeaders
                .AsNoTracking()
                .Include(o => o.OriginalOrder)
                .Include(o => o.Payments)
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.IsRefund);

            if (filter.ShiftId.HasValue)
                refundsQuery = refundsQuery.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                refundsQuery = refundsQuery.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                refundsQuery = refundsQuery.Where(o => o.CreationUser == filter.CashierUsername);

            var refunds = await refundsQuery
                .OrderByDescending(o => o.CreationDate)
                .ToListAsync(ct);

            var refundItems = refunds.Select(o =>
            {
                var payMethod = o.Payments.FirstOrDefault()?.PaymentMethod;
                var payName = payMethod.HasValue ? GetPaymentMethodNames(payMethod.Value).Ar : "نقداً";

                return new PosRefundOrderItemDto
                {
                    RefundOrderId = o.Id,
                    RefundOrderNumber = o.OrderNumber,
                    RefundInvoiceNumber = o.InvoiceNumber,
                    OriginalOrderId = o.OriginalOrderId,
                    OriginalOrderNumber = o.OriginalOrder?.OrderNumber,
                    OriginalInvoiceNumber = o.OriginalOrder?.InvoiceNumber,
                    RefundAmount = Math.Abs(o.TotalAmount),
                    RefundReason = o.RefundReason,
                    RefundUser = o.CreationUser,
                    RefundDate = o.CreationDate,
                    PaymentMethodName = payName
                };
            }).ToList();

            var result = new PosVoidsAndRefundsReportDto
            {
                TotalVoidCount = voidLines.Count,
                TotalVoidAmount = voidLines.Sum(v => v.LineTotal),
                TotalRefundCount = refundItems.Count,
                TotalRefundAmount = refundItems.Sum(r => r.RefundAmount),
                VoidLines = voidLines,
                RefundOrders = refundItems
            };

            return ApiResponse<PosVoidsAndRefundsReportDto>.CreateSuccess(result, "تم إنشاء تقرير الإلغاءات والمرتجعات بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير الإلغاءات والمرتجعات للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosVoidsAndRefundsReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير الإلغاءات والمرتجعات: " + ex.Message);
        }
    }
    #endregion

    #region 7. الخصومات وتعديل الأسعار - Discounts & Price Adjustments
    public async Task<ApiResponse<PosDiscountsReportDto>> GetDiscountsReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var ordersQuery = _context.PosOrderHeaders
                .AsNoTracking()
                .Include(o => o.Lines)
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided &&
                            !o.IsRefund &&
                            (o.DiscountAmount > 0 || o.Lines.Any(l => l.DiscountAmount > 0)));

            if (filter.ShiftId.HasValue)
                ordersQuery = ordersQuery.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                ordersQuery = ordersQuery.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                ordersQuery = ordersQuery.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await ordersQuery.ToListAsync(ct);

            var discountDetails = new List<PosDiscountDetailItemDto>();

            foreach (var o in orders)
            {
                if (o.DiscountAmount > 0)
                {
                    discountDetails.Add(new PosDiscountDetailItemDto
                    {
                        OrderId = o.Id,
                        OrderNumber = o.OrderNumber,
                        InvoiceNumber = o.InvoiceNumber,
                        CashierUsername = o.CreationUser,
                        OrderDate = o.CreationDate,
                        Scope = "فاتورة (Order)",
                        ItemName = "كامل الفاتورة",
                        DiscountReason = o.DiscountReason ?? "خصم يدوي / عام",
                        DiscountPercent = o.DiscountPercent,
                        DiscountAmount = o.DiscountAmount,
                        SubtotalAmount = o.SubtotalAmount,
                        TotalAmount = o.TotalAmount
                    });
                }

                foreach (var line in o.Lines.Where(l => !l.IsVoided && l.DiscountAmount > 0))
                {
                    discountDetails.Add(new PosDiscountDetailItemDto
                    {
                        OrderId = o.Id,
                        OrderNumber = o.OrderNumber,
                        InvoiceNumber = o.InvoiceNumber,
                        CashierUsername = o.CreationUser,
                        OrderDate = o.CreationDate,
                        Scope = "صنف (Line)",
                        ItemName = line.ItemName,
                        DiscountReason = line.DiscountReason ?? o.DiscountReason ?? "خصم صنف",
                        DiscountPercent = line.DiscountPercent,
                        DiscountAmount = line.DiscountAmount,
                        SubtotalAmount = line.Quantity * line.UnitPrice,
                        TotalAmount = line.LineTotal
                    });
                }
            }

            var byReason = discountDetails
                .GroupBy(d => string.IsNullOrWhiteSpace(d.DiscountReason) ? "غير محدد" : d.DiscountReason)
                .Select(g => new PosDiscountByReasonDto
                {
                    Reason = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.DiscountAmount)
                })
                .OrderByDescending(r => r.TotalAmount)
                .ToList();

            var byCashier = discountDetails
                .GroupBy(d => d.CashierUsername)
                .Select(g => new PosDiscountByCashierDto
                {
                    CashierUsername = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.DiscountAmount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            var totalDiscount = orders.Sum(o => o.DiscountAmount + o.Lines.Where(l => !l.IsVoided).Sum(l => l.DiscountAmount));

            var result = new PosDiscountsReportDto
            {
                TotalDiscountAmount = totalDiscount,
                TotalOrdersWithDiscount = orders.Count,
                AverageDiscountPerOrder = orders.Count > 0 ? Math.Round(totalDiscount / orders.Count, 2) : 0m,
                DiscountsByReason = byReason,
                DiscountsByCashier = byCashier,
                DiscountDetails = discountDetails.OrderByDescending(d => d.OrderDate).ToList()
            };

            return ApiResponse<PosDiscountsReportDto>.CreateSuccess(result, "تم إنشاء تقرير الخصومات وتعديل الأسعار بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير الخصومات للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosDiscountsReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير الخصومات: " + ex.Message);
        }
    }
    #endregion

    #region 8. فروقات الصندوق - Cash Drawer Variance
    public async Task<ApiResponse<PosCashDrawerVarianceReportDto>> GetCashDrawerVarianceReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosShifts
                .AsNoTracking()
                .Include(s => s.Till)
                .Where(s => s.BranchId == filter.BranchId &&
                            s.OpenedAt >= start &&
                            s.OpenedAt <= end);

            if (filter.ShiftId.HasValue)
                query = query.Where(s => s.Id == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(s => s.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(s => s.CreationUser == filter.CashierUsername);

            var shifts = await query
                .OrderByDescending(s => s.OpenedAt)
                .ToListAsync(ct);

            var shiftItems = shifts.Select(s =>
            {
                var cashVariance = s.VarianceAmount ?? (s.CountedCashAmount.HasValue ? s.CountedCashAmount.Value - s.ExpectedCashInDrawer : 0m);
                var cardVariance = s.CardVarianceAmount ?? (s.CountedCardAmount.HasValue ? s.CountedCardAmount.Value - s.ExpectedCardAmount : 0m);

                return new PosShiftVarianceItemDto
                {
                    ShiftId = s.Id,
                    ShiftNumber = s.ShiftNumber,
                    TillId = s.TillId,
                    TillName = s.Till?.TillName,
                    CashierUsername = s.CreationUser,
                    Status = s.Status.ToString(),
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt ?? s.BlindClosedAt,
                    OpeningFloat = s.OpeningFloat,
                    TotalCashSales = s.TotalCashSales,
                    TotalCashRefunds = s.TotalCashRefunds,
                    TotalFloatIn = s.TotalFloatIn,
                    TotalCashDrop = s.TotalCashDrop,
                    TotalPayOut = s.TotalPayOut,
                    TotalTipPayout = s.TotalTipPayout,
                    ExpectedCashInDrawer = s.ExpectedCashInDrawer,
                    CountedCashAmount = s.CountedCashAmount,
                    VarianceAmount = cashVariance,
                    ExpectedCardAmount = s.ExpectedCardAmount,
                    CountedCardAmount = s.CountedCardAmount,
                    CardVarianceAmount = cardVariance,
                    IsAudited = s.IsAudited,
                    AuditedBy = s.AuditedBy,
                    AuditNotes = s.AuditNotes
                };
            }).ToList();

            var shortage = shiftItems.Where(s => s.VarianceAmount < 0).Sum(s => Math.Abs(s.VarianceAmount!.Value));
            var overage = shiftItems.Where(s => s.VarianceAmount > 0).Sum(s => s.VarianceAmount!.Value);
            var netVariance = overage - shortage;

            var result = new PosCashDrawerVarianceReportDto
            {
                TotalShiftsAudited = shiftItems.Count(s => s.CountedCashAmount.HasValue),
                TotalExpectedCash = shiftItems.Sum(s => s.ExpectedCashInDrawer),
                TotalCountedCash = shiftItems.Sum(s => s.CountedCashAmount ?? 0m),
                TotalShortageAmount = shortage,
                TotalOverageAmount = overage,
                NetVarianceAmount = netVariance,
                NetCardVarianceAmount = shiftItems.Sum(s => s.CardVarianceAmount ?? 0m),
                Shifts = shiftItems
            };

            return ApiResponse<PosCashDrawerVarianceReportDto>.CreateSuccess(result, "تم إنشاء تقرير فروقات الصندوق بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير فروقات الصندوق للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosCashDrawerVarianceReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير فروقات الصندوق: " + ex.Message);
        }
    }
    #endregion

    #region 9. الإيداعات والسحوبات النقدية - Cash In / Cash Out
    public async Task<ApiResponse<PosCashMovementReportDto>> GetCashMovementsReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosShiftCashMovements
                .AsNoTracking()
                .Include(m => m.Shift)
                .Where(m => m.Shift != null &&
                            m.Shift.BranchId == filter.BranchId &&
                            m.CreationDate >= start &&
                            m.CreationDate <= end);

            if (filter.ShiftId.HasValue)
                query = query.Where(m => m.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(m => m.Shift!.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(m => m.CreationUser == filter.CashierUsername);

            var movements = await query
                .OrderByDescending(m => m.CreationDate)
                .ToListAsync(ct);

            var items = movements.Select(m =>
            {
                var (moveAr, moveEn) = GetCashMovementNames(m.MovementType);
                var voucherAr = m.VoucherType == PosVoucherType.Receipt ? "سند قبض" : "سند صرف";

                return new PosCashMovementDetailDto
                {
                    Id = m.Id,
                    ShiftId = m.ShiftId,
                    ShiftNumber = m.Shift?.ShiftNumber ?? string.Empty,
                    MovementType = m.MovementType,
                    MovementTypeNameAr = moveAr,
                    MovementTypeNameEn = moveEn,
                    VoucherType = m.VoucherType,
                    VoucherTypeNameAr = voucherAr,
                    VoucherNumber = m.VoucherNumber,
                    Amount = m.Amount,
                    Reason = m.Reason,
                    BeneficiaryName = m.BeneficiaryName,
                    ApprovedBy = m.ApprovedBy,
                    CreationUser = m.CreationUser,
                    CreationDate = m.CreationDate
                };
            }).ToList();

            var floatIn = items.Where(x => x.MovementType == PosCashMovementType.FloatIn).Sum(x => x.Amount);
            var cashDrop = items.Where(x => x.MovementType == PosCashMovementType.CashDrop).Sum(x => x.Amount);
            var payOut = items.Where(x => x.MovementType == PosCashMovementType.PayOut).Sum(x => x.Amount);
            var tipPayout = items.Where(x => x.MovementType == PosCashMovementType.TipPayout).Sum(x => x.Amount);
            var net = floatIn - (cashDrop + payOut + tipPayout);

            var result = new PosCashMovementReportDto
            {
                TotalFloatIn = floatIn,
                TotalCashDrop = cashDrop,
                TotalPayOut = payOut,
                TotalTipPayout = tipPayout,
                NetCashMovement = net,
                Movements = items
            };

            return ApiResponse<PosCashMovementReportDto>.CreateSuccess(result, "تم إنشاء تقرير الإيداعات والسحوبات النقدية بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير حركات الصندوق للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosCashMovementReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير حركات الصندوق: " + ex.Message);
        }
    }
    #endregion

    #region 10. المبيعات حسب الساعة - Hourly Sales
    public async Task<ApiResponse<PosHourlySalesReportDto>> GetHourlySalesReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided &&
                            !o.IsRefund);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);

            var totalSales = orders.Sum(o => o.TotalAmount);
            var totalOrders = orders.Count;

            var hourSlots = new List<PosHourlySlotDto>();
            for (int h = 0; h < 24; h++)
            {
                var inHour = orders.Where(o => o.CreationDate.Hour == h).ToList();
                var hSales = inHour.Sum(o => o.TotalAmount);
                var hOrders = inHour.Count;
                var hCovers = inHour.Sum(o => o.Covers);
                var pct = totalSales > 0 ? Math.Round((hSales / totalSales) * 100m, 2) : 0m;
                var avg = hOrders > 0 ? Math.Round(hSales / hOrders, 2) : 0m;

                hourSlots.Add(new PosHourlySlotDto
                {
                    Hour = h,
                    HourFormatted = $"{h:D2}:00 - {(h + 1) % 24:D2}:00",
                    OrderCount = hOrders,
                    TotalSales = hSales,
                    AverageTicket = avg,
                    Covers = hCovers,
                    PercentageOfTotal = pct
                });
            }

            var peak = hourSlots.OrderByDescending(x => x.TotalSales).FirstOrDefault();

            var result = new PosHourlySalesReportDto
            {
                PeakHour = peak?.Hour ?? 0,
                PeakHourSales = peak?.TotalSales ?? 0m,
                PeakHourFormatted = peak?.HourFormatted ?? "00:00 - 01:00",
                TotalDaySales = totalSales,
                TotalOrders = totalOrders,
                Hours = hourSlots
            };

            return ApiResponse<PosHourlySalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات بالساعة بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات بالساعة للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosHourlySalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات الساعة: " + ex.Message);
        }
    }
    #endregion

    #region 11. المبيعات حسب الطابق والطاولة - Sales by Floor and Table
    public async Task<ApiResponse<PosFloorTableSalesReportDto>> GetSalesByFloorAndTableReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Include(o => o.Table)
                    .ThenInclude(t => t!.Floor)
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided &&
                            !o.IsRefund &&
                            o.TableId != null);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);

            var grouped = orders
                .GroupBy(o => o.TableId!.Value)
                .Select(g =>
                {
                    var table = g.FirstOrDefault()?.Table;
                    var floor = table?.Floor;

                    var tableSales = g.Sum(o => o.TotalAmount);
                    var orderCount = g.Count();
                    var covers = g.Sum(o => o.Covers);
                    var avgTicket = orderCount > 0 ? Math.Round(tableSales / orderCount, 2) : 0m;
                    var avgPerCover = covers > 0 ? Math.Round(tableSales / covers, 2) : 0m;

                    return new PosTableSalesItemDto
                    {
                        FloorId = floor?.Id ?? 0,
                        FloorName = floor?.FloorName ?? "طابق رئيسي",
                        TableId = g.Key,
                        TableNumber = table?.TableNumber ?? g.Key.ToString(),
                        TableName = table?.TableName,
                        Capacity = table?.Capacity ?? 4,
                        OrderCount = orderCount,
                        CoversCount = covers,
                        TotalSales = tableSales,
                        AverageTicket = avgTicket,
                        AveragePerCover = avgPerCover
                    };
                })
                .OrderByDescending(t => t.TotalSales)
                .ToList();

            var totalSales = grouped.Sum(t => t.TotalSales);
            var totalCovers = grouped.Sum(t => t.CoversCount);

            var result = new PosFloorTableSalesReportDto
            {
                TotalTableOrders = grouped.Sum(t => t.OrderCount),
                TotalTableSales = totalSales,
                TotalCovers = totalCovers,
                AverageSpendPerCover = totalCovers > 0 ? Math.Round(totalSales / totalCovers, 2) : 0m,
                Tables = grouped
            };

            return ApiResponse<PosFloorTableSalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب الطوابق والطاولات بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير مبيعات الطاولات للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosFloorTableSalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات الطاولات: " + ex.Message);
        }
    }
    #endregion

    #region 12. المبيعات حسب نوع الطلب - Sales by Order Type
    public async Task<ApiResponse<PosOrderTypeSalesReportDto>> GetSalesByOrderTypeReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided &&
                            !o.IsRefund);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);
            var totalSales = orders.Sum(o => o.TotalAmount);

            var grouped = orders
                .GroupBy(o => o.OrderType)
                .Select(g =>
                {
                    var (ar, en) = GetOrderTypeNames(g.Key);
                    var count = g.Count();
                    var subtotal = g.Sum(o => o.SubtotalAmount);
                    var discount = g.Sum(o => o.DiscountAmount);
                    var tax = g.Sum(o => o.TaxAmount);
                    var delivery = g.Sum(o => o.DeliveryFee);
                    var total = g.Sum(o => o.TotalAmount);
                    var avg = count > 0 ? Math.Round(total / count, 2) : 0m;
                    var pct = totalSales > 0 ? Math.Round((total / totalSales) * 100m, 2) : 0m;

                    return new PosOrderTypeDetailDto
                    {
                        OrderType = g.Key,
                        OrderTypeCode = (int)g.Key,
                        OrderTypeNameAr = ar,
                        OrderTypeNameEn = en,
                        OrderCount = count,
                        SubtotalAmount = subtotal,
                        DiscountAmount = discount,
                        TaxAmount = tax,
                        DeliveryFee = delivery,
                        TotalAmount = total,
                        AverageOrderValue = avg,
                        Percentage = pct
                    };
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            var result = new PosOrderTypeSalesReportDto
            {
                TotalOrdersCount = orders.Count,
                TotalSales = totalSales,
                OrderTypes = grouped
            };

            return ApiResponse<PosOrderTypeSalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب نوع الطلب بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير المبيعات حسب نوع الطلب للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosOrderTypeSalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير أنواع الطلبات: " + ex.Message);
        }
    }
    #endregion

    #region 13. المبيعات حسب العميل - Sales by Customer
    public async Task<ApiResponse<PosCustomerSalesReportDto>> GetSalesByCustomerReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderHeaders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided &&
                            !o.IsRefund);

            if (filter.ShiftId.HasValue)
                query = query.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await query.ToListAsync(ct);

            var grouped = orders
                .GroupBy(o => o.CustomerId)
                .Select(g =>
                {
                    var cust = g.FirstOrDefault()?.Customer;
                    var isWalkIn = !g.Key.HasValue;
                    var name = isWalkIn ? "زبون نقدي / عام (Walk-in)" : (cust?.NameLocal ?? g.FirstOrDefault()?.CustomerName ?? "عميل غير مسمى");
                    var code = cust?.CustomerCode ?? (isWalkIn ? "WALK-IN" : "CUST-" + g.Key);
                    var phone = cust?.Phone;

                    var count = g.Count();
                    var total = g.Sum(o => o.TotalAmount);
                    var discount = g.Sum(o => o.DiscountAmount);
                    var paid = g.Sum(o => o.PaidAmount);
                    var remaining = Math.Max(0m, total - paid);
                    var avg = count > 0 ? Math.Round(total / count, 2) : 0m;
                    var lastVisit = g.Max(o => o.CreationDate);

                    return new PosCustomerSalesItemDto
                    {
                        CustomerId = g.Key,
                        CustomerCode = code,
                        CustomerName = name,
                        PhoneNumber = phone,
                        OrderCount = count,
                        TotalSales = total,
                        DiscountAmount = discount,
                        PaidAmount = paid,
                        RemainingAmount = remaining,
                        AverageTicket = avg,
                        LastVisitDate = lastVisit
                    };
                })
                .OrderByDescending(c => c.TotalSales)
                .ToList();

            var result = new PosCustomerSalesReportDto
            {
                TotalCustomers = grouped.Count,
                TotalCustomerSales = grouped.Sum(x => x.TotalSales),
                Customers = grouped
            };

            return ApiResponse<PosCustomerSalesReportDto>.CreateSuccess(result, "تم إنشاء تقرير المبيعات حسب العملاء بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير مبيعات العملاء للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosCustomerSalesReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير مبيعات العملاء: " + ex.Message);
        }
    }
    #endregion

    #region 14. ملخص الضريبة - Tax Summary
    public async Task<ApiResponse<PosTaxSummaryReportDto>> GetTaxSummaryReportAsync(PosReportFilterDto filter, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var ordersQuery = _context.PosOrderHeaders
                .AsNoTracking()
                .Include(o => o.Taxes)
                .Where(o => o.BranchId == filter.BranchId &&
                            o.CreationDate >= start &&
                            o.CreationDate <= end &&
                            o.IsActive &&
                            o.Status != PosOrderStatus.Voided);

            if (filter.ShiftId.HasValue)
                ordersQuery = ordersQuery.Where(o => o.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                ordersQuery = ordersQuery.Where(o => o.TillId == filter.TillId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                ordersQuery = ordersQuery.Where(o => o.CreationUser == filter.CashierUsername);

            var orders = await ordersQuery.ToListAsync(ct);

            var nonRefund = orders.Where(o => !o.IsRefund).ToList();
            var refund = orders.Where(o => o.IsRefund).ToList();

            var grossSales = nonRefund.Sum(o => o.SubtotalAmount);
            var allTaxes = nonRefund.SelectMany(o => o.Taxes).ToList();
            var refundTaxes = refund.SelectMany(o => o.Taxes).ToList();

            var taxCollected = nonRefund.Sum(o => o.TaxAmount);
            var refundTax = refund.Sum(o => Math.Abs(o.TaxAmount));
            var netTax = taxCollected - refundTax;

            var rateBreakdown = allTaxes
                .GroupBy(t => new { t.TaxRateId, t.TaxRateCode, t.TaxPercent, t.IsInclusive })
                .Select(g => new PosTaxRateBreakdownDto
                {
                    TaxRateId = g.Key.TaxRateId,
                    TaxRateCode = g.Key.TaxRateCode,
                    TaxPercent = g.Key.TaxPercent,
                    TaxableAmount = g.Sum(t => t.TaxableAmount),
                    TaxAmount = g.Sum(t => t.TaxAmount),
                    IsInclusive = g.Key.IsInclusive
                })
                .OrderBy(r => r.TaxPercent)
                .ToList();

            var totalTaxable = rateBreakdown.Where(r => r.TaxPercent > 0).Sum(r => r.TaxableAmount);
            var totalZeroRated = rateBreakdown.Where(r => r.TaxPercent == 0).Sum(r => r.TaxableAmount);

            var result = new PosTaxSummaryReportDto
            {
                TotalGrossSales = grossSales,
                TotalTaxableSales = totalTaxable,
                TotalZeroRatedSales = totalZeroRated,
                TotalExemptSales = Math.Max(0m, grossSales - totalTaxable - totalZeroRated),
                TotalTaxCollected = taxCollected,
                TotalRefundTax = refundTax,
                NetTaxPayable = netTax,
                TaxRatesBreakdown = rateBreakdown
            };

            return ApiResponse<PosTaxSummaryReportDto>.CreateSuccess(result, "تم إنشاء تقرير ملخص الضريبة بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير ملخص الضريبة للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosTaxSummaryReportDto>.CreateFailure("حدث خطأ أثناء استخراج ملخص الضريبة: " + ex.Message);
        }
    }
    #endregion

    #region 15. الطاولات المفتوحة مباشر - Open Tables Live
    public async Task<ApiResponse<PosOpenTablesLiveReportDto>> GetOpenTablesLiveReportAsync(long branchId, long? floorId = null, CancellationToken ct = default)
    {
        try
        {
            var query = _context.PosTables
                .AsNoTracking()
                .Include(t => t.Floor)
                .Include(t => t.ActiveOrder)
                .Where(t => t.Floor != null && t.Floor.BranchId == branchId && t.IsActive);

            if (floorId.HasValue)
                query = query.Where(t => t.FloorId == floorId.Value);

            var tables = await query.OrderBy(t => t.Floor!.SortOrder).ThenBy(t => t.TableNumber).ToListAsync(ct);

            var now = DateTime.UtcNow;

            var tableItems = tables.Select(t =>
            {
                var openAt = t.ActiveOrder?.CreationDate ?? t.StatusChangedAt;
                var duration = openAt.HasValue ? (int)(now - openAt.Value).TotalMinutes : 0;
                var currentTotal = t.ActiveOrder?.TotalAmount ?? 0m;
                var covers = t.ActiveOrder?.Covers ?? 0;

                return new PosOpenTableItemDto
                {
                    TableId = t.Id,
                    TableNumber = t.TableNumber,
                    TableName = t.TableName,
                    FloorId = t.FloorId,
                    FloorName = t.Floor?.FloorName ?? string.Empty,
                    Capacity = t.Capacity,
                    Status = t.Status,
                    StatusNameAr = GetTableStatusNameAr(t.Status),
                    ActiveOrderId = t.ActiveOrderId,
                    OrderNumber = t.ActiveOrder?.OrderNumber,
                    Covers = covers,
                    OpenedAt = openAt,
                    DurationMinutes = duration,
                    CurrentTotalAmount = currentTotal,
                    WaiterOrCashier = t.ActiveOrder?.CreationUser
                };
            }).ToList();

            var totalTables = tableItems.Count;
            var occupied = tableItems.Count(t => t.Status == PosTableStatus.Occupied);
            var billed = tableItems.Count(t => t.Status == PosTableStatus.Billed);
            var available = tableItems.Count(t => t.Status == PosTableStatus.Available);
            var occupancyRate = totalTables > 0 ? Math.Round(((decimal)(occupied + billed) / totalTables) * 100m, 2) : 0m;
            var activeSales = tableItems.Sum(t => t.CurrentTotalAmount);

            var result = new PosOpenTablesLiveReportDto
            {
                TotalTables = totalTables,
                AvailableTablesCount = available,
                OccupiedTablesCount = occupied,
                BilledTablesCount = billed,
                OccupancyRatePercent = occupancyRate,
                ActiveDiningSalesTotal = activeSales,
                OpenTables = tableItems
            };

            return ApiResponse<PosOpenTablesLiveReportDto>.CreateSuccess(result, "تم جلب بيانات الطاولات المفتوحة والمباشرة بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء جلب الطاولات المفتوحة المباشرة للفرع {BranchId}", branchId);
            return ApiResponse<PosOpenTablesLiveReportDto>.CreateFailure("حدث خطأ أثناء جلب الطاولات المفتوحة: " + ex.Message);
        }
    }
    #endregion

    #region 16. زمن تحضير المطبخ - Kitchen Prep Time
    public async Task<ApiResponse<PosKitchenPrepTimeReportDto>> GetKitchenPrepTimeReportAsync(PosReportFilterDto filter, int delayedThresholdMinutes = 15, CancellationToken ct = default)
    {
        try
        {
            var (start, end) = NormalizeDateRange(filter.StartDate, filter.EndDate);

            var query = _context.PosOrderLines
                .AsNoTracking()
                .Include(l => l.Order)
                .Where(l => l.Order != null &&
                            l.Order.BranchId == filter.BranchId &&
                            l.Order.CreationDate >= start &&
                            l.Order.CreationDate <= end &&
                            l.Order.IsActive &&
                            l.Order.Status != PosOrderStatus.Voided &&
                            !l.IsVoided &&
                            l.KdsSentAt != null &&
                            l.KdsReadyAt != null &&
                            l.KdsReadyAt >= l.KdsSentAt);

            if (filter.ShiftId.HasValue)
                query = query.Where(l => l.Order!.ShiftId == filter.ShiftId.Value);
            if (filter.TillId.HasValue)
                query = query.Where(l => l.Order!.TillId == filter.TillId.Value);

            if (!string.IsNullOrWhiteSpace(filter.CashierUsername))
                query = query.Where(l => l.Order!.CreationUser == filter.CashierUsername);

            var lines = await query.ToListAsync(ct);

            var lineStats = lines.Select(l => new
            {
                l.ItemId,
                l.ItemCode,
                l.ItemName,
                Station = string.IsNullOrWhiteSpace(l.PrepStation) ? "المطبخ الرئيسي (Main Kitchen)" : l.PrepStation,
                PrepMinutes = (decimal)(l.KdsReadyAt!.Value - l.KdsSentAt!.Value).TotalMinutes
            }).ToList();

            var stationStats = lineStats
                .GroupBy(s => s.Station)
                .Select(g =>
                {
                    var itemsCount = g.Count();
                    var avg = itemsCount > 0 ? Math.Round(g.Average(x => x.PrepMinutes), 2) : 0m;
                    var min = itemsCount > 0 ? Math.Round(g.Min(x => x.PrepMinutes), 2) : 0m;
                    var max = itemsCount > 0 ? Math.Round(g.Max(x => x.PrepMinutes), 2) : 0m;
                    var delayed = g.Count(x => x.PrepMinutes > delayedThresholdMinutes);

                    return new PosPrepStationStatDto
                    {
                        PrepStation = g.Key,
                        ItemsCount = itemsCount,
                        AveragePrepMinutes = avg,
                        MinPrepMinutes = min,
                        MaxPrepMinutes = max,
                        DelayedCount = delayed
                    };
                })
                .OrderByDescending(s => s.AveragePrepMinutes)
                .ToList();

            var slowestItems = lineStats
                .GroupBy(s => new { s.ItemId, s.ItemCode, s.ItemName, s.Station })
                .Select(g => new PosItemPrepStatDto
                {
                    ItemId = g.Key.ItemId,
                    ItemCode = g.Key.ItemCode,
                    ItemName = g.Key.ItemName,
                    PrepStation = g.Key.Station,
                    ItemsCount = g.Count(),
                    AveragePrepMinutes = Math.Round(g.Average(x => x.PrepMinutes), 2)
                })
                .OrderByDescending(i => i.AveragePrepMinutes)
                .Take(20)
                .ToList();

            var totalAnalyzed = lineStats.Count;
            var overallAvg = totalAnalyzed > 0 ? Math.Round(lineStats.Average(x => x.PrepMinutes), 2) : 0m;
            var totalDelayed = lineStats.Count(x => x.PrepMinutes > delayedThresholdMinutes);

            var result = new PosKitchenPrepTimeReportDto
            {
                TotalItemsAnalyzed = totalAnalyzed,
                OverallAveragePrepMinutes = overallAvg,
                DelayedOrdersThresholdMinutes = delayedThresholdMinutes,
                TotalDelayedItemsCount = totalDelayed,
                Stations = stationStats,
                SlowestItems = slowestItems
            };

            return ApiResponse<PosKitchenPrepTimeReportDto>.CreateSuccess(result, "تم إنشاء تقرير أزمنة تحضير المطبخ بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء إنشاء تقرير زمن تحضير المطبخ للفرع {BranchId}", filter.BranchId);
            return ApiResponse<PosKitchenPrepTimeReportDto>.CreateFailure("حدث خطأ أثناء استخراج تقرير تحضير المطبخ: " + ex.Message);
        }
    }
    #endregion

    #region Helper Translations
    private static (string Ar, string En) GetPaymentMethodNames(PosPaymentMethod method) => method switch
    {
        PosPaymentMethod.Cash => ("نقداً", "Cash"),
        PosPaymentMethod.Card => ("بطاقة مدى / ائتمان", "Card"),
        PosPaymentMethod.CustomerAccount => ("حساب عميل (آجل / ذمم)", "Customer Account"),
        PosPaymentMethod.Cheque => ("شيك", "Cheque"),
        PosPaymentMethod.BankTransfer => ("تحويل بنكي", "Bank Transfer"),
        PosPaymentMethod.DigitalWallet => ("محفظة إلكترونية", "Digital Wallet"),
        PosPaymentMethod.LoyaltyPoints => ("نقاط ولاء", "Loyalty Points"),
        PosPaymentMethod.GiftCard => ("بطاقة هدايا", "Gift Card"),
        PosPaymentMethod.Hospitality => ("ضيافة", "Hospitality"),
        PosPaymentMethod.Split => ("دفع متعدد", "Split"),
        PosPaymentMethod.AggregatorPaid => ("تطبيقات التوصيل", "Aggregator Paid"),
        _ => (method.ToString(), method.ToString())
    };

    private static (string Ar, string En) GetOrderTypeNames(PosOrderType type) => type switch
    {
        PosOrderType.DineIn => ("محلي / صالة", "Dine In"),
        PosOrderType.Takeaway => ("سفري / استلام", "Takeaway"),
        PosOrderType.Delivery => ("توصيل", "Delivery"),
        PosOrderType.Aggregator => ("تطبيقات التوصيل", "Aggregator"),
        PosOrderType.Kiosk => ("جهاز الخدمة الذاتية (Kiosk)", "Kiosk"),
        PosOrderType.QrTable => ("طلب طاولة باركود QR", "QR Table"),
        _ => (type.ToString(), type.ToString())
    };

    private static (string Ar, string En) GetCashMovementNames(PosCashMovementType type) => type switch
    {
        PosCashMovementType.FloatIn => ("إيداع نقدي / عهدة إضافية", "Float In"),
        PosCashMovementType.CashDrop => ("توريد خزينة / سحب أرباح", "Cash Drop"),
        PosCashMovementType.PayOut => ("مصروفات نثرية / سحب", "Pay Out"),
        PosCashMovementType.TipPayout => ("صرف إكراميات موظفين", "Tip Payout"),
        _ => (type.ToString(), type.ToString())
    };

    private static string GetTableStatusNameAr(PosTableStatus status) => status switch
    {
        PosTableStatus.Available => "متاح",
        PosTableStatus.Occupied => "مشغول",
        PosTableStatus.Billed => "تم إصدار الفاتورة",
        PosTableStatus.Cleaning => "قيد التنظيف",
        PosTableStatus.Reserved => "محجوز",
        PosTableStatus.OutOfService => "خارج الخدمة",
        _ => status.ToString()
    };
    #endregion
}
