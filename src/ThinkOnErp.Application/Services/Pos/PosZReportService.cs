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
    Task<ApiResponse<PagedResultDto<ZReportSummaryDto>>> GetZReportsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
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

    public PosZReportService(
        IPosZReportRepository zReportRepository,
        IPosShiftRepository shiftRepository,
        IPosOrderRepository orderRepository,
        IPosAuditService auditService,
        IInvStockLedgerService stockLedgerService,
        ISysSettingRepository settingRepo,
        IInvWarehouseRepository warehouseRepo,
        ILogger<PosZReportService> logger)
    {
        _zReportRepository = zReportRepository;
        _shiftRepository = shiftRepository;
        _orderRepository = orderRepository;
        _auditService = auditService;
        _stockLedgerService = stockLedgerService;
        _settingRepo = settingRepo;
        _warehouseRepo = warehouseRepo;
        _logger = logger;
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
        }

        var nextSeq = await _zReportRepository.GetNextZSequenceNumberAsync(dto.BranchId, ct);
        var orders = await _orderRepository.GetActiveOrdersAsync(dto.BranchId, null, ct);
        var shiftOrders = dto.ShiftId.HasValue ? orders.Where(o => o.ShiftId == dto.ShiftId.Value).ToList() : orders.ToList();

        var salesOrders = shiftOrders.Where(o => !o.IsRefund).ToList();
        var refundOrders = shiftOrders.Where(o => o.IsRefund).ToList();

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

            CashPaymentsTotal = shift?.TotalCashSales ?? 0m,
            CardPaymentsTotal = shift?.TotalCardSales ?? 0m,
            CustomerAccountPaymentsTotal = 0m,
            OtherPaymentsTotal = shift?.TotalOtherSales ?? 0m,

            OpeningFloat = shift?.OpeningFloat ?? 0m,
            CashDropTotal = shift?.TotalCashDrop ?? 0m,
            PayOutTotal = shift?.TotalPayOut ?? 0m,
            ExpectedDrawerCash = shift?.ExpectedCashInDrawer ?? 0m,
            ActualCountedCash = shift?.CountedCashAmount ?? 0m,
            CashVariance = shift?.VarianceAmount ?? 0m,

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

        return await GetZReportByIdAsync(zReport.Id, ct);
    }

    public async Task<ApiResponse<ZReportSummaryDto>> GetZReportByIdAsync(long id, CancellationToken ct = default)
    {
        var z = await _zReportRepository.GetByIdAsync(id, ct);
        if (z == null)
            return ApiResponse<ZReportSummaryDto>.CreateFailure("Z-Report not found", null, 404);

        return ApiResponse<ZReportSummaryDto>.CreateSuccess(MapToDto(z));
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
            OtherPaymentsTotal = z.OtherPaymentsTotal,
            OpeningFloat = z.OpeningFloat,
            CashDropTotal = z.CashDropTotal,
            PayOutTotal = z.PayOutTotal,
            ExpectedDrawerCash = z.ExpectedDrawerCash,
            ActualCountedCash = z.ActualCountedCash,
            CashVariance = z.CashVariance,
            IsPostedToGl = z.IsPostedToGl,
            GeneratedBy = z.GeneratedBy
        };
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

            // Group sales lines by ItemId and UomId
            var salesLines = shiftOrders
                .Where(o => !o.IsRefund && o.IsPaid)
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

            // Group refund lines by ItemId and UomId for restock
            var refundLines = shiftOrders
                .Where(o => o.IsRefund && o.IsPaid)
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
        var whSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId);
        if (whSetting != null && long.TryParse(whSetting.SettingValue, out var whId) && whId > 0)
        {
            return whId;
        }

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

        return 61; // Default fallback POS warehouse
    }
}
