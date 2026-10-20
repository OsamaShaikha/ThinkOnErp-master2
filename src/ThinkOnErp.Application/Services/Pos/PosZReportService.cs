using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosZReportService
{
    Task<ApiResponse<ZReportSummaryDto>> GenerateZReportAsync(GenerateZReportDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<ZReportSummaryDto>> GetZReportByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<ZReportSummaryDto>> GetZReportByShiftIdAsync(long shiftId, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<ZReportSummaryDto>>> GetZReportsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ApiResponse<XReportSummaryDto>> GenerateXReportAsync(long branchId, long? shiftId, long? tillId, string username, CancellationToken ct = default);
}

public class PosZReportService : IPosZReportService
{
    private readonly IPosZReportRepository _zReportRepository;
    private readonly IPosShiftRepository _shiftRepository;
    private readonly IPosOrderRepository _orderRepository;
    private readonly IPosAuditService _auditService;
    private readonly IInvStockLedgerService _stockLedgerService;
    private readonly ISysSettingRepository _settingRepo;
    private readonly IInvWarehouseRepository _warehouseRepo;
    private readonly ILogger<PosZReportService> _logger;
    private readonly IPosBranchSettingRepository? _branchSettingRepository;

    public PosZReportService(
        IPosZReportRepository zReportRepository,
        IPosShiftRepository shiftRepository,
        IPosOrderRepository orderRepository,
        IPosAuditService auditService,
        IInvStockLedgerService stockLedgerService,
        ISysSettingRepository settingRepo,
        IInvWarehouseRepository warehouseRepo,
        ILogger<PosZReportService> logger,
        IPosBranchSettingRepository? branchSettingRepository = null)
    {
        _zReportRepository = zReportRepository;
        _shiftRepository = shiftRepository;
        _orderRepository = orderRepository;
        _auditService = auditService;
        _stockLedgerService = stockLedgerService;
        _settingRepo = settingRepo;
        _warehouseRepo = warehouseRepo;
        _logger = logger;
        _branchSettingRepository = branchSettingRepository;
    }

    public async Task<ApiResponse<ZReportSummaryDto>> GenerateZReportAsync(GenerateZReportDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<ZReportSummaryDto>.CreateFailure("Request body cannot be null", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<ZReportSummaryDto>.CreateFailure("Valid BranchId is required", null, 400);

        PosShift? shift = null;
        if (dto.ShiftId.HasValue)
        {
            shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId.Value, ct);
            if (shift == null)
                return ApiResponse<ZReportSummaryDto>.CreateFailure("Shift not found", null, 404);

            var existingZReport = await _zReportRepository.GetByShiftIdAsync(dto.ShiftId.Value, ct);
            if (existingZReport != null)
            {
                return await GetZReportByShiftIdAsync(dto.ShiftId.Value, ct);
            }
        }

        var nextSeq = await _zReportRepository.GetNextZSequenceNumberAsync(dto.BranchId, ct);
        var rawOrders = await _orderRepository.GetOrdersForZReportAsync(dto.BranchId, dto.ShiftId, ct);
        if (rawOrders == null)
        {
            var fallback = await _orderRepository.GetActiveOrdersAsync(dto.BranchId, null, ct) ?? Array.Empty<PosOrderHeader>();
            rawOrders = dto.ShiftId.HasValue ? fallback.Where(o => o.ShiftId == dto.ShiftId.Value).ToList() : fallback.ToList();
        }
        var shiftOrders = rawOrders.ToList();

        var salesOrders = shiftOrders.Where(o => !o.IsRefund).ToList();
        var refundOrders = shiftOrders.Where(o => o.IsRefund).ToList();

        var allPayments = shiftOrders.SelectMany(o => o.Payments).ToList();

        var cashSales = shift?.TotalCashSales ?? allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount);
        var cardSales = shift?.TotalCardSales ?? allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Card || p.PaymentMethod == PosPaymentMethod.DigitalWallet).Sum(p => p.Amount);
        var customerAccountSales = (shift?.TotalCustomerAccountSales > 0 ? shift.TotalCustomerAccountSales : 0m);
        if (customerAccountSales == 0m)
            customerAccountSales = allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount).Sum(p => p.Amount);

        var hospitalitySales = (shift?.TotalHospitalitySales > 0 ? shift.TotalHospitalitySales : 0m);
        if (hospitalitySales == 0m)
            hospitalitySales = allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Hospitality).Sum(p => p.Amount);

        var otherSales = shift?.TotalOtherSales ?? allPayments.Where(p => p.PaymentMethod != PosPaymentMethod.Cash && p.PaymentMethod != PosPaymentMethod.Card && p.PaymentMethod != PosPaymentMethod.DigitalWallet && p.PaymentMethod != PosPaymentMethod.CustomerAccount && p.PaymentMethod != PosPaymentMethod.Hospitality).Sum(p => p.Amount);

        var cardExpected = shift?.ExpectedCardAmount ?? cardSales;
        var cardActual = shift?.CountedCardAmount ?? cardExpected;
        var cardVariance = shift?.CardVarianceAmount ?? (cardActual - cardExpected);

        var zReport = new PosZReport
        {
            BranchId = dto.BranchId,
            TillId = dto.TillId ?? shift?.TillId,
            ShiftId = dto.ShiftId,
            ZSequenceNumber = nextSeq,
            ReportDate = DateTime.UtcNow,
            FirstInvoiceNumber = shiftOrders.OrderBy(o => o.CreationDate).FirstOrDefault()?.InvoiceNumber ?? "N/A",
            LastInvoiceNumber = shiftOrders.OrderByDescending(o => o.CreationDate).FirstOrDefault()?.InvoiceNumber ?? "N/A",
            TotalInvoiceCount = salesOrders.Count,
            TotalReturnCount = refundOrders.Count,

            GrossSalesAmount = salesOrders.Sum(o => o.SubtotalAmount),
            TotalDiscountAmount = salesOrders.Sum(o => o.DiscountAmount),
            NetSalesAmount = salesOrders.Sum(o => o.SubtotalAmount - o.DiscountAmount),
            TotalTaxAmount = salesOrders.Sum(o => o.TaxAmount) - refundOrders.Sum(o => Math.Abs(o.TaxAmount)),
            TotalServiceCharge = salesOrders.Sum(o => o.ServiceChargeAmount),
            TotalRefundAmount = refundOrders.Sum(o => Math.Abs(o.TotalAmount)),
            FinalTotalAmount = salesOrders.Sum(o => o.TotalAmount) - refundOrders.Sum(o => Math.Abs(o.TotalAmount)),

            CashPaymentsTotal = cashSales,
            CardPaymentsTotal = cardSales,
            CustomerAccountPaymentsTotal = customerAccountSales,
            HospitalityPaymentsTotal = hospitalitySales,
            OtherPaymentsTotal = otherSales,

            OpeningFloat = shift?.OpeningFloat ?? 0m,
            CashDropTotal = shift?.TotalCashDrop ?? 0m,
            PayOutTotal = shift?.TotalPayOut ?? 0m,
            ExpectedDrawerCash = shift?.ExpectedCashInDrawer ?? (cashSales + (shift?.OpeningFloat ?? 0m)),
            ActualCountedCash = shift?.CountedCashAmount ?? (shift?.ExpectedCashInDrawer ?? cashSales),
            CashVariance = shift?.VarianceAmount ?? 0m,

            ExpectedCardAmount = cardExpected,
            ActualCountedCard = cardActual,
            CardVariance = cardVariance,

            IsPostedToGl = false,
            GeneratedBy = username,
            CreationDate = DateTime.UtcNow
        };

        await _zReportRepository.AddZReportAsync(zReport, ct);
        await _zReportRepository.SaveChangesAsync(ct);

        await DeductShiftConsolidatedStockIfConfiguredAsync(shiftOrders, zReport, username, ct);

        await _auditService.LogZReportGeneratedAsync(
            zReport.BranchId,
            zReport.Id,
            zReport.ShiftId ?? 0,
            zReport.ZSequenceNumber.ToString(),
            zReport.GrossSalesAmount,
            zReport.TotalTaxAmount,
            zReport.CashVariance,
            username,
            ct);

        var resultDto = MapToDto(zReport);
        resultDto.OrderTypeBreakdowns = BuildOrderTypeBreakdowns(salesOrders, zReport.NetSalesAmount);
        resultDto.TaxBreakdown = BuildTaxBreakdown(salesOrders, refundOrders);
        resultDto.CashMovementSummary = BuildCashMovementSummary(shift, cashSales, refundOrders.Sum(o => o.Payments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount)));
        resultDto.OperationalStats = BuildOperationalStats(shift, salesOrders, refundOrders, zReport.NetSalesAmount);
        resultDto.CategorySales = BuildCategorySales(salesOrders, zReport.NetSalesAmount);

        return ApiResponse<ZReportSummaryDto>.CreateSuccess(resultDto);
    }

    public async Task<ApiResponse<XReportSummaryDto>> GenerateXReportAsync(
        long branchId,
        long? shiftId,
        long? tillId,
        string username,
        CancellationToken ct = default)
    {
        if (branchId <= 0)
            return ApiResponse<XReportSummaryDto>.CreateFailure("Valid BranchId is required", null, 400);

        PosShift? shift = null;
        if (shiftId.HasValue)
        {
            shift = await _shiftRepository.GetShiftByIdAsync(shiftId.Value, ct);
        }
        else if (tillId.HasValue)
        {
            shift = await _shiftRepository.GetActiveShiftByTillAsync(tillId.Value, ct);
        }
        else
        {
            var pagedShifts = await _shiftRepository.GetShiftsPagedAsync(branchId, status: PosShiftStatus.Open, pageIndex: 1, pageSize: 1, ct: ct);
            shift = pagedShifts.Items.FirstOrDefault();
        }

        if (shift == null)
            return ApiResponse<XReportSummaryDto>.CreateFailure("No active open shift found for the specified branch or till", null, 404);

        var rawOrders = await _orderRepository.GetOrdersForZReportAsync(branchId, shift.Id, ct);
        var shiftOrders = (rawOrders ?? Array.Empty<PosOrderHeader>()).ToList();

        var salesOrders = shiftOrders.Where(o => !o.IsRefund).ToList();
        var refundOrders = shiftOrders.Where(o => o.IsRefund).ToList();
        var allPayments = shiftOrders.SelectMany(o => o.Payments).ToList();

        var cashSales = shift.TotalCashSales > 0 ? shift.TotalCashSales : allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount);
        var cardSales = shift.TotalCardSales > 0 ? shift.TotalCardSales : allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Card || p.PaymentMethod == PosPaymentMethod.DigitalWallet).Sum(p => p.Amount);
        var customerAccountSales = shift.TotalCustomerAccountSales > 0 ? shift.TotalCustomerAccountSales : allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount).Sum(p => p.Amount);
        var hospitalitySales = shift.TotalHospitalitySales > 0 ? shift.TotalHospitalitySales : allPayments.Where(p => p.PaymentMethod == PosPaymentMethod.Hospitality).Sum(p => p.Amount);
        var otherSales = shift.TotalOtherSales > 0 ? shift.TotalOtherSales : allPayments.Where(p => p.PaymentMethod != PosPaymentMethod.Cash && p.PaymentMethod != PosPaymentMethod.Card && p.PaymentMethod != PosPaymentMethod.DigitalWallet && p.PaymentMethod != PosPaymentMethod.CustomerAccount && p.PaymentMethod != PosPaymentMethod.Hospitality).Sum(p => p.Amount);

        var cardExpected = shift.ExpectedCardAmount > 0 ? shift.ExpectedCardAmount : cardSales;
        var cardActual = shift.CountedCardAmount;
        var cardVariance = shift.CardVarianceAmount;

        var netSales = salesOrders.Sum(o => o.SubtotalAmount - o.DiscountAmount);

        var xReport = new XReportSummaryDto
        {
            ReportType = "X-REPORT",
            ReportTitle = "تقرير X - قراءة لحظية للوردية (Live Snapshot)",
            IsLiveSnapshot = true,
            BranchId = branchId,
            ShiftId = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            ShiftStatus = (int)shift.Status,
            ShiftStatusName = shift.Status switch
            {
                PosShiftStatus.Open => "مفتوحة (Open)",
                PosShiftStatus.Suspended => "معلقة (Suspended)",
                PosShiftStatus.BlindClosed => "إغلاق أعمى (Blind Closed)",
                PosShiftStatus.AuditedAndClosed => "مغلقة ومعتمدة (Audited & Closed)",
                _ => shift.Status.ToString()
            },
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = username,

            GrossSalesAmount = salesOrders.Sum(o => o.SubtotalAmount),
            TotalDiscountAmount = salesOrders.Sum(o => o.DiscountAmount),
            NetSalesAmount = netSales,
            TotalTaxAmount = salesOrders.Sum(o => o.TaxAmount) - refundOrders.Sum(o => Math.Abs(o.TaxAmount)),
            TotalServiceCharge = salesOrders.Sum(o => o.ServiceChargeAmount),
            TotalRefundAmount = refundOrders.Sum(o => Math.Abs(o.TotalAmount)),
            FinalTotalAmount = salesOrders.Sum(o => o.TotalAmount) - refundOrders.Sum(o => Math.Abs(o.TotalAmount)),

            CashPaymentsTotal = cashSales,
            CardPaymentsTotal = cardSales,
            CustomerAccountPaymentsTotal = customerAccountSales,
            HospitalityPaymentsTotal = hospitalitySales,
            OtherPaymentsTotal = otherSales,

            OpeningFloat = shift.OpeningFloat,
            CashDropTotal = shift.TotalCashDrop,
            PayOutTotal = shift.TotalPayOut,
            ExpectedDrawerCash = shift.ExpectedCashInDrawer,
            ActualCountedCash = shift.CountedCashAmount,
            CashVariance = shift.VarianceAmount,

            ExpectedCardAmount = cardExpected,
            ActualCountedCard = cardActual,
            CardVariance = cardVariance,

            TenderReconciliations = BuildShiftTenderReconciliations(shift, cashSales, cardSales, customerAccountSales, hospitalitySales, otherSales),
            OrderTypeBreakdowns = BuildOrderTypeBreakdowns(salesOrders, netSales),
            TaxBreakdown = BuildTaxBreakdown(salesOrders, refundOrders),
            CashMovementSummary = BuildCashMovementSummary(shift, cashSales, refundOrders.Sum(o => o.Payments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount))),
            OperationalStats = BuildOperationalStats(shift, salesOrders, refundOrders, netSales),
            CategorySales = BuildCategorySales(salesOrders, netSales)
        };

        return ApiResponse<XReportSummaryDto>.CreateSuccess(xReport);
    }

    public async Task<ApiResponse<ZReportSummaryDto>> GetZReportByIdAsync(long id, CancellationToken ct = default)
    {
        var z = await _zReportRepository.GetByIdAsync(id, ct);
        if (z == null)
            return ApiResponse<ZReportSummaryDto>.CreateFailure("Z-Report not found", null, 404);

        var dto = MapToDto(z);
        if (z.ShiftId.HasValue)
        {
            var rawOrders = await _orderRepository.GetOrdersForZReportAsync(z.BranchId, z.ShiftId, ct);
            if (rawOrders != null)
            {
                var shiftOrders = rawOrders.ToList();
                var salesOrders = shiftOrders.Where(o => !o.IsRefund).ToList();
                var refundOrders = shiftOrders.Where(o => o.IsRefund).ToList();
                var shift = z.Shift ?? await _shiftRepository.GetShiftByIdAsync(z.ShiftId.Value, ct);

                dto.OrderTypeBreakdowns = BuildOrderTypeBreakdowns(salesOrders, z.NetSalesAmount);
                dto.TaxBreakdown = BuildTaxBreakdown(salesOrders, refundOrders);
                dto.CashMovementSummary = BuildCashMovementSummary(shift, z.CashPaymentsTotal, refundOrders.Sum(o => o.Payments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount)));
                dto.OperationalStats = BuildOperationalStats(shift, salesOrders, refundOrders, z.NetSalesAmount);
                dto.CategorySales = BuildCategorySales(salesOrders, z.NetSalesAmount);
            }
        }

        return ApiResponse<ZReportSummaryDto>.CreateSuccess(dto);
    }

    public async Task<ApiResponse<ZReportSummaryDto>> GetZReportByShiftIdAsync(long shiftId, CancellationToken ct = default)
    {
        var z = await _zReportRepository.GetByShiftIdAsync(shiftId, ct);
        if (z == null)
            return ApiResponse<ZReportSummaryDto>.CreateFailure("Z-Report not found for specified shift", null, 404);

        var dto = MapToDto(z);

        var rawOrders = await _orderRepository.GetOrdersForZReportAsync(z.BranchId, z.ShiftId, ct);
        if (rawOrders == null)
        {
            var fallback = await _orderRepository.GetActiveOrdersAsync(z.BranchId, null, ct) ?? Array.Empty<PosOrderHeader>();
            rawOrders = fallback.Where(o => o.ShiftId == shiftId).ToList();
        }
        var shiftOrders = rawOrders.ToList();
        var salesOrders = shiftOrders.Where(o => !o.IsRefund).ToList();
        var refundOrders = shiftOrders.Where(o => o.IsRefund).ToList();
        var shift = z.Shift ?? await _shiftRepository.GetShiftByIdAsync(shiftId, ct);

        dto.OrderTypeBreakdowns = BuildOrderTypeBreakdowns(salesOrders, z.NetSalesAmount);
        dto.TaxBreakdown = BuildTaxBreakdown(salesOrders, refundOrders);
        dto.CashMovementSummary = BuildCashMovementSummary(shift, z.CashPaymentsTotal, refundOrders.Sum(o => o.Payments.Where(p => p.PaymentMethod == PosPaymentMethod.Cash).Sum(p => p.Amount)));
        dto.OperationalStats = BuildOperationalStats(shift, salesOrders, refundOrders, z.NetSalesAmount);
        dto.CategorySales = BuildCategorySales(salesOrders, z.NetSalesAmount);

        return ApiResponse<ZReportSummaryDto>.CreateSuccess(dto);
    }

    public async Task<ApiResponse<PagedResultDto<ZReportSummaryDto>>> GetZReportsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _zReportRepository.GetZReportsPagedAsync(
            branchId, fromDate, toDate, pageIndex, pageSize, ct);

        var dtos = items.Select(MapToDto).ToList();
        var paged = new PagedResultDto<ZReportSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<ZReportSummaryDto>>.CreateSuccess(paged);
    }

    private static ZReportSummaryDto MapToDto(PosZReport z)
    {
        return new ZReportSummaryDto
        {
            Id = z.Id,
            BranchId = z.BranchId,
            ZSequenceNumber = z.ZSequenceNumber,
            ReportDate = z.ReportDate,
            FirstInvoiceNumber = z.FirstInvoiceNumber,
            LastInvoiceNumber = z.LastInvoiceNumber,
            TotalInvoiceCount = z.TotalInvoiceCount,
            TotalReturnCount = z.TotalReturnCount,
            GrossSalesAmount = z.GrossSalesAmount,
            TotalDiscountAmount = z.TotalDiscountAmount,
            NetSalesAmount = z.NetSalesAmount,
            TotalTaxAmount = z.TotalTaxAmount,
            TotalServiceCharge = z.TotalServiceCharge,
            TotalRefundAmount = z.TotalRefundAmount,
            FinalTotalAmount = z.FinalTotalAmount,
            CashPaymentsTotal = z.CashPaymentsTotal,
            CardPaymentsTotal = z.CardPaymentsTotal,
            CustomerAccountPaymentsTotal = z.CustomerAccountPaymentsTotal,
            HospitalityPaymentsTotal = z.HospitalityPaymentsTotal,
            OtherPaymentsTotal = z.OtherPaymentsTotal,
            OpeningFloat = z.OpeningFloat,
            CashDropTotal = z.CashDropTotal,
            PayOutTotal = z.PayOutTotal,
            ExpectedDrawerCash = z.ExpectedDrawerCash,
            ActualCountedCash = z.ActualCountedCash,
            CashVariance = z.CashVariance,
            ExpectedCardAmount = z.ExpectedCardAmount,
            ActualCountedCard = z.ActualCountedCard,
            CardVariance = z.CardVariance,
            TenderReconciliations = BuildZReportTenderReconciliations(z),
            TaxBreakdown = new PosTaxBreakdownSummaryDto
            {
                TaxableAmountStandardRate = z.NetSalesAmount,
                StandardRateTaxAmount = z.TotalTaxAmount,
                ZeroRatedAmount = 0m,
                ExemptAmount = 0m,
                TotalTaxAmount = z.TotalTaxAmount
            },
            CashMovementSummary = new PosCashMovementSummaryDto
            {
                OpeningFloat = z.OpeningFloat,
                CashSales = z.CashPaymentsTotal,
                CashRefunds = 0m,
                FloatInReceiptVouchers = z.Shift?.TotalFloatIn ?? 0m,
                PayOutPaymentVouchers = z.PayOutTotal,
                CashDropTotal = z.CashDropTotal,
                ExpectedDrawerCash = z.ExpectedDrawerCash,
                ActualCountedCash = z.ActualCountedCash,
                CashVariance = z.CashVariance
            },
            OperationalStats = new PosOperationalStatsDto
            {
                TotalInvoiceCount = z.TotalInvoiceCount,
                TotalReturnCount = z.TotalReturnCount,
                TotalGuestsCovers = 0,
                AverageTicketAmount = z.TotalInvoiceCount > 0 ? Math.Round(z.NetSalesAmount / z.TotalInvoiceCount, 2) : 0m,
                AverageSpendPerGuest = z.TotalInvoiceCount > 0 ? Math.Round(z.NetSalesAmount / z.TotalInvoiceCount, 2) : 0m,
                FirstInvoiceNumber = z.FirstInvoiceNumber,
                LastInvoiceNumber = z.LastInvoiceNumber,
                ShiftOpenedAt = z.Shift?.OpenedAt,
                ReportGeneratedAt = z.ReportDate,
                TillName = z.Till?.TillName,
                CashierName = z.GeneratedBy,
                ShiftNumber = z.Shift?.ShiftNumber
            },
            IsPostedToGl = z.IsPostedToGl,
            GeneratedBy = z.GeneratedBy
        };
    }

    private static List<PosTenderReconciliationDto> BuildZReportTenderReconciliations(PosZReport z)
    {
        var list = new List<PosTenderReconciliationDto>();

        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Cash,
            PaymentMethodName = "نقدي (Cash)",
            ExpectedAmount = z.ExpectedDrawerCash,
            CountedAmount = z.ActualCountedCash,
            VarianceAmount = z.CashVariance,
            IsAutoDeclared = false,
            Notes = z.CashVariance == 0 ? "متطابق" : (z.CashVariance > 0 ? "فائض نقدي" : "عجز نقدي")
        });

        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Card,
            PaymentMethodName = "شبكة / مدى (Cards)",
            ExpectedAmount = z.ExpectedCardAmount,
            CountedAmount = z.ActualCountedCard,
            VarianceAmount = z.CardVariance,
            IsAutoDeclared = false,
            Notes = z.CardVariance == 0 ? "متطابق مع إيصال الشبكة" : (z.CardVariance > 0 ? "فائض بالشبكة" : "عجز بالشبكة")
        });

        if (z.CustomerAccountPaymentsTotal > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.CustomerAccount,
                PaymentMethodName = "ذمم عملاء (Customer Account)",
                ExpectedAmount = z.CustomerAccountPaymentsTotal,
                CountedAmount = z.CustomerAccountPaymentsTotal,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من فواتير الذمم المسجلة"
            });
        }

        if (z.HospitalityPaymentsTotal > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.Hospitality,
                PaymentMethodName = "ضيافة (Hospitality)",
                ExpectedAmount = z.HospitalityPaymentsTotal,
                CountedAmount = z.HospitalityPaymentsTotal,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من طلبات الضيافة المسجلة"
            });
        }

        if (z.OtherPaymentsTotal > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = 0,
                PaymentMethodName = "أخرى (Other)",
                ExpectedAmount = z.OtherPaymentsTotal,
                CountedAmount = z.OtherPaymentsTotal,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من المبيعات الأخرى"
            });
        }

        return list;
    }

    private static List<PosTenderReconciliationDto> BuildShiftTenderReconciliations(
        PosShift shift,
        decimal cashSales,
        decimal cardSales,
        decimal customerAccountSales,
        decimal hospitalitySales,
        decimal otherSales)
    {
        var list = new List<PosTenderReconciliationDto>();

        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Cash,
            PaymentMethodName = "نقدي (Cash)",
            ExpectedAmount = shift.ExpectedCashInDrawer,
            CountedAmount = shift.CountedCashAmount ?? shift.ExpectedCashInDrawer,
            VarianceAmount = shift.VarianceAmount ?? 0m,
            IsAutoDeclared = false,
            Notes = shift.VarianceAmount == 0 || !shift.VarianceAmount.HasValue ? "متطابق" : (shift.VarianceAmount > 0 ? "فائض نقدي" : "عجز نقدي")
        });

        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Card,
            PaymentMethodName = "شبكة / مدى (Cards)",
            ExpectedAmount = shift.ExpectedCardAmount > 0 ? shift.ExpectedCardAmount : cardSales,
            CountedAmount = shift.CountedCardAmount ?? (shift.ExpectedCardAmount > 0 ? shift.ExpectedCardAmount : cardSales),
            VarianceAmount = shift.CardVarianceAmount ?? 0m,
            IsAutoDeclared = false,
            Notes = shift.CardVarianceAmount == 0 || !shift.CardVarianceAmount.HasValue ? "متطابق مع إيصال الشبكة" : (shift.CardVarianceAmount > 0 ? "فائض بالشبكة" : "عجز بالشبكة")
        });

        if (customerAccountSales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.CustomerAccount,
                PaymentMethodName = "ذمم عملاء (Customer Account)",
                ExpectedAmount = customerAccountSales,
                CountedAmount = customerAccountSales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من فواتير الذمم المسجلة"
            });
        }

        if (hospitalitySales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.Hospitality,
                PaymentMethodName = "ضيافة (Hospitality)",
                ExpectedAmount = hospitalitySales,
                CountedAmount = hospitalitySales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من طلبات الضيافة المسجلة"
            });
        }

        if (otherSales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = 0,
                PaymentMethodName = "أخرى (Other)",
                ExpectedAmount = otherSales,
                CountedAmount = otherSales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من المبيعات الأخرى"
            });
        }

        return list;
    }

    private static List<PosOrderTypeBreakdownDto> BuildOrderTypeBreakdowns(List<PosOrderHeader> salesOrders, decimal netSales)
    {
        var list = new List<PosOrderTypeBreakdownDto>();
        if (salesOrders == null || salesOrders.Count == 0) return list;

        var grouped = salesOrders.GroupBy(o => o.OrderType);
        foreach (var g in grouped)
        {
            var typeTotal = g.Sum(o => o.TotalAmount);
            var percent = netSales > 0 ? Math.Round((typeTotal / netSales) * 100m, 2) : 0m;
            string typeName = g.Key switch
            {
                PosOrderType.DineIn => "محلي / صالة (Dine-In)",
                PosOrderType.Takeaway => "سفري (Takeaway)",
                PosOrderType.Delivery => "توصيل (Delivery)",
                PosOrderType.Aggregator => "تطبيقات التوصيل (Aggregator)",
                PosOrderType.Kiosk => "جهاز الخدمة الذاتية (Kiosk)",
                PosOrderType.QrTable => "طاولة عبر الباركود (QR Table)",
                _ => g.Key.ToString()
            };

            list.Add(new PosOrderTypeBreakdownDto
            {
                OrderType = (int)g.Key,
                OrderTypeName = typeName,
                OrdersCount = g.Count(),
                TotalAmount = typeTotal,
                PercentageOfTotal = percent
            });
        }
        return list;
    }

    private static PosTaxBreakdownSummaryDto BuildTaxBreakdown(List<PosOrderHeader> salesOrders, List<PosOrderHeader> refundOrders)
    {
        var salesTax = salesOrders.Sum(o => o.TaxAmount);
        var refundTax = refundOrders.Sum(o => Math.Abs(o.TaxAmount));
        var totalTax = salesTax - refundTax;
        var netTaxable = salesOrders.Sum(o => o.SubtotalAmount - o.DiscountAmount) - refundOrders.Sum(o => Math.Abs(o.SubtotalAmount - o.DiscountAmount));

        return new PosTaxBreakdownSummaryDto
        {
            TaxableAmountStandardRate = netTaxable,
            StandardRateTaxAmount = totalTax,
            ZeroRatedAmount = 0m,
            ExemptAmount = 0m,
            TotalTaxAmount = totalTax
        };
    }

    private static PosCashMovementSummaryDto BuildCashMovementSummary(PosShift? shift, decimal cashSales, decimal cashRefunds)
    {
        var openingFloat = shift?.OpeningFloat ?? 0m;
        var floatIn = shift?.TotalFloatIn ?? 0m;
        var payOut = shift?.TotalPayOut ?? 0m;
        var drops = shift?.TotalCashDrop ?? 0m;
        var expected = shift?.ExpectedCashInDrawer ?? (openingFloat + cashSales - cashRefunds + floatIn - payOut - drops);
        var counted = shift?.CountedCashAmount ?? expected;
        var variance = shift?.VarianceAmount ?? (counted - expected);

        return new PosCashMovementSummaryDto
        {
            OpeningFloat = openingFloat,
            CashSales = cashSales,
            CashRefunds = cashRefunds,
            FloatInReceiptVouchers = floatIn,
            PayOutPaymentVouchers = payOut,
            CashDropTotal = drops,
            ExpectedDrawerCash = expected,
            ActualCountedCash = counted,
            CashVariance = variance
        };
    }

    private static PosOperationalStatsDto BuildOperationalStats(
        PosShift? shift,
        List<PosOrderHeader> salesOrders,
        List<PosOrderHeader> refundOrders,
        decimal netSales)
    {
        var orderCount = salesOrders.Count;
        var refundCount = refundOrders.Count;
        var totalCovers = salesOrders.Sum(o => o.Covers);
        var avgTicket = orderCount > 0 ? Math.Round(netSales / orderCount, 2) : 0m;
        var avgGuest = totalCovers > 0 ? Math.Round(netSales / totalCovers, 2) : avgTicket;

        return new PosOperationalStatsDto
        {
            TotalInvoiceCount = orderCount,
            TotalReturnCount = refundCount,
            TotalGuestsCovers = totalCovers,
            AverageTicketAmount = avgTicket,
            AverageSpendPerGuest = avgGuest,
            FirstInvoiceNumber = salesOrders.OrderBy(o => o.CreationDate).FirstOrDefault()?.InvoiceNumber ?? "N/A",
            LastInvoiceNumber = salesOrders.OrderByDescending(o => o.CreationDate).FirstOrDefault()?.InvoiceNumber ?? "N/A",
            ShiftOpenedAt = shift?.OpenedAt,
            ReportGeneratedAt = DateTime.UtcNow,
            TillName = shift?.Till?.TillName,
            CashierName = shift?.CashierUser?.FullNameLocal ?? shift?.CashierUser?.UserName ?? shift?.CreationUser,
            ShiftNumber = shift?.ShiftNumber
        };
    }

    private static List<PosCategorySalesSummaryDto> BuildCategorySales(List<PosOrderHeader> salesOrders, decimal netSales)
    {
        var list = new List<PosCategorySalesSummaryDto>();
        if (salesOrders == null || salesOrders.Count == 0) return list;

        var allLines = salesOrders.SelectMany(o => o.Lines).Where(l => !l.IsVoided).ToList();
        if (allLines.Count == 0) return list;

        var grouped = allLines.GroupBy(l => !string.IsNullOrWhiteSpace(l.PrepStation) ? l.PrepStation : (!string.IsNullOrWhiteSpace(l.ItemName) ? l.ItemName : "أخرى"));
        foreach (var g in grouped)
        {
            var totalAmt = g.Sum(l => (l.Quantity * l.UnitPrice) - l.DiscountAmount);
            var totalQty = g.Sum(l => l.Quantity);
            var percent = netSales > 0 ? Math.Round((totalAmt / netSales) * 100m, 2) : 0m;

            list.Add(new PosCategorySalesSummaryDto
            {
                CategoryName = g.Key,
                QuantitySold = totalQty,
                TotalSalesAmount = totalAmt,
                PercentageOfTotal = percent
            });
        }

        return list.OrderByDescending(c => c.TotalSalesAmount).Take(10).ToList();
    }

    private async Task DeductShiftConsolidatedStockIfConfiguredAsync(
        List<PosOrderHeader> shiftOrders,
        PosZReport zReport,
        string username,
        CancellationToken ct)
    {
        try
        {
            var setting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode);
            if (setting == null || !int.TryParse(setting.SettingValue, out var mode) || mode != SysCodeKeys.PosStockDeductionModes.Consolidated)
            {
                return; // Only process if mode is explicitly Consolidated (2)
            }

            var warehouseId = await ResolvePosWarehouseIdAsync(zReport.BranchId, ct);
            if (warehouseId <= 0)
            {
                _logger.LogWarning("No active warehouse found for consolidated POS stock deduction in branch {BranchId}", zReport.BranchId);
                return;
            }

            var salesLines = shiftOrders
                .Where(o => !o.IsRefund && (o.IsPaid || o.Status == PosOrderStatus.Completed))
                .SelectMany(o => o.Lines)
                .Where(l => !l.IsVoided && l.Quantity > 0)
                .GroupBy(l => new { l.ItemId, l.UomId });

            foreach (var g in salesLines)
            {
                var totalQty = g.Sum(l => l.Quantity);
                var moveReq = new StockMovementRequestDto
                {
                    ItemId = g.Key.ItemId,
                    WarehouseId = warehouseId,
                    Quantity = totalQty,
                    UomCode = g.Key.UomId > 0 ? g.Key.UomId : 1,
                    TransactionType = SysCodeKeys.StockTransactionTypes.PosSales, // 1004
                    SourceDocId = $"Z-REP-{zReport.ZSequenceNumber}",
                    SourceDocType = "POS_ZREPORT",
                    SourceModule = "POS",
                    Notes = $"Consolidated POS deduction for Shift #{zReport.ShiftId}, Z-Report #{zReport.ZSequenceNumber}"
                };

                var postResult = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                if (!postResult.Success)
                {
                    _logger.LogWarning("Consolidated stock deduction warning for Item {ItemId}: {Message}", g.Key.ItemId, postResult.Message);
                }
            }

            var refundLines = shiftOrders
                .Where(o => o.IsRefund && (o.IsPaid || o.Status == PosOrderStatus.Completed))
                .SelectMany(o => o.Lines)
                .Where(l => !l.IsVoided && Math.Abs(line_qty(l)) > 0)
                .GroupBy(l => new { l.ItemId, l.UomId });

            foreach (var g in refundLines)
            {
                var totalQty = g.Sum(l => Math.Abs(l.Quantity));
                var moveReq = new StockMovementRequestDto
                {
                    ItemId = g.Key.ItemId,
                    WarehouseId = warehouseId,
                    Quantity = totalQty,
                    UomCode = g.Key.UomId > 0 ? g.Key.UomId : 1,
                    TransactionType = SysCodeKeys.StockTransactionTypes.SalesReturnRestock, // 1501
                    SourceDocId = $"Z-REP-{zReport.ZSequenceNumber}",
                    SourceDocType = "POS_ZREPORT_REFUND",
                    SourceModule = "POS",
                    Notes = $"Consolidated POS restock for Shift #{zReport.ShiftId}, Z-Report #{zReport.ZSequenceNumber}"
                };

                var postResult = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                if (!postResult.Success)
                {
                    _logger.LogWarning("Consolidated stock restock warning for Item {ItemId}: {Message}", g.Key.ItemId, postResult.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform consolidated POS stock deduction for Z-Report {ZReportId}", zReport.Id);
        }
    }

    private static decimal line_qty(PosOrderLine line) => line.Quantity;

    private async Task<long> ResolvePosWarehouseIdAsync(long branchId, CancellationToken ct)
    {
        // 1. Branch-specific POS setting
        if (_branchSettingRepository != null)
        {
            try
            {
                var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(branchId, ct);
                if (branchSetting?.DefaultWarehouseId != null && branchSetting.DefaultWarehouseId.Value > 0)
                {
                    return branchSetting.DefaultWarehouseId.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get branch setting for branch {BranchId}", branchId);
            }
        }

        // 2. Global setting 51
        var whSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId);
        if (whSetting != null && long.TryParse(whSetting.SettingValue, out var whId) && whId > 0)
        {
            return whId;
        }

        // 3. Active warehouse for this branch
        try
        {
            var warehouses = await _warehouseRepo.GetAllByBranchAsync(branchId, ct);
            var activeWh = warehouses.FirstOrDefault(w => w.IsActive);
            if (activeWh != null) return activeWh.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching warehouses for branch {BranchId}", branchId);
        }

        return 101; // Default fallback POS warehouse
    }
}
