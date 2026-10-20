using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class PosShiftAndZReportReconciliationTests
{
    private readonly Mock<IPosShiftRepository> _shiftRepo = new();
    private readonly Mock<IPosOrderRepository> _orderRepo = new();
    private readonly Mock<IPosAuditService> _auditService = new();
    private readonly Mock<IPosZReportRepository> _zReportRepo = new();
    private readonly Mock<IInvStockLedgerService> _stockLedgerService = new();
    private readonly Mock<ISysSettingRepository> _settingRepo = new();
    private readonly Mock<IInvWarehouseRepository> _warehouseRepo = new();
    private readonly Mock<IPosBranchSettingRepository> _branchSettingRepo = new();
    private readonly Mock<ILogger<PosShiftService>> _shiftLogger = new();
    private readonly Mock<ILogger<PosZReportService>> _zReportLogger = new();

    private readonly PosShiftService _shiftService;
    private readonly PosZReportService _zReportService;

    public PosShiftAndZReportReconciliationTests()
    {
        _shiftService = new PosShiftService(
            _shiftRepo.Object,
            _auditService.Object,
            settingRepo: _settingRepo.Object,
            logger: _shiftLogger.Object,
            branchSettingRepository: _branchSettingRepo.Object);
        _zReportService = new PosZReportService(
            _zReportRepo.Object,
            _shiftRepo.Object,
            _orderRepo.Object,
            _auditService.Object,
            _stockLedgerService.Object,
            _settingRepo.Object,
            _warehouseRepo.Object,
            _zReportLogger.Object);
    }

    [Fact]
    public async Task BlindCloseShift_CalculatesCashAndCardVariance_AndAutoPopulatesTenderReconciliations()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 1,
            BranchId = 10,
            TillId = 5,
            ShiftNumber = "SH-001",
            Status = PosShiftStatus.Open,
            OpeningFloat = 100m,
            TotalCashSales = 500m,
            TotalCardSales = 300m,
            TotalCustomerAccountSales = 150m,
            TotalHospitalitySales = 75m,
            TotalOtherSales = 0m,
            TotalCashRefunds = 20m,
            TotalCardRefunds = 0m,
            TotalCashDrop = 50m,
            TotalPayOut = 30m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.HasParkedOrdersAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Expected Cash in Drawer = 100 (float) + 500 (sales) - 20 (refund) - 50 (drop) - 30 (payout) = 500
        // Expected Card = 300
        var blindCloseDto = new BlindCloseShiftDto
        {
            ShiftId = 1,
            CountedCashAmount = 510m, // Cashier has +10 surplus cash
            CountedCardAmount = 300m, // Exactly matched card terminal slip
            ClosingNotes = "Shift closed successfully"
        };

        // Act
        var response = await _shiftService.BlindCloseShiftAsync(blindCloseDto, "cashier1");

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var summary = response.Data;
        Assert.Equal(PosShiftStatus.BlindClosed, summary.Status);
        Assert.Equal(500m, summary.ExpectedCashInDrawer);
        Assert.Equal(510m, summary.CountedCashAmount);
        Assert.Equal(10m, summary.VarianceAmount); // +10 cash surplus

        Assert.Equal(300m, summary.ExpectedCardAmount);
        Assert.Equal(300m, summary.CountedCardAmount);
        Assert.Equal(0m, summary.CardVarianceAmount); // 0 card variance

        Assert.Equal(150m, summary.TotalCustomerAccountSales);
        Assert.Equal(75m, summary.TotalHospitalitySales);

        // Check Tender Reconciliations breakdown
        Assert.NotEmpty(summary.TenderReconciliations);

        var cashRec = summary.TenderReconciliations.First(t => t.PaymentMethod == (int)PosPaymentMethod.Cash);
        Assert.Equal(500m, cashRec.ExpectedAmount);
        Assert.Equal(510m, cashRec.CountedAmount);
        Assert.Equal(10m, cashRec.VarianceAmount);
        Assert.False(cashRec.IsAutoDeclared);

        var cardRec = summary.TenderReconciliations.First(t => t.PaymentMethod == (int)PosPaymentMethod.Card);
        Assert.Equal(300m, cardRec.ExpectedAmount);
        Assert.Equal(300m, cardRec.CountedAmount);
        Assert.Equal(0m, cardRec.VarianceAmount);
        Assert.False(cardRec.IsAutoDeclared);

        var customerAccountRec = summary.TenderReconciliations.First(t => t.PaymentMethod == (int)PosPaymentMethod.CustomerAccount);
        Assert.Equal(150m, customerAccountRec.ExpectedAmount);
        Assert.Equal(150m, customerAccountRec.CountedAmount);
        Assert.Equal(0m, customerAccountRec.VarianceAmount);
        Assert.True(customerAccountRec.IsAutoDeclared);

        var hospitalityRec = summary.TenderReconciliations.First(t => t.PaymentMethod == (int)PosPaymentMethod.Hospitality);
        Assert.Equal(75m, hospitalityRec.ExpectedAmount);
        Assert.Equal(75m, hospitalityRec.CountedAmount);
        Assert.Equal(0m, hospitalityRec.VarianceAmount);
        Assert.True(hospitalityRec.IsAutoDeclared);
    }

    [Fact]
    public async Task GenerateZReport_GeneratesCompleteTenderBreakdown_AndReconciliation()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 10,
            BranchId = 1,
            TillId = 2,
            ShiftNumber = "SH-10",
            Status = PosShiftStatus.BlindClosed,
            OpeningFloat = 50m,
            TotalCashSales = 200m,
            TotalCardSales = 400m,
            TotalCustomerAccountSales = 80m,
            TotalHospitalitySales = 30m,
            TotalCashRefunds = 0m,
            TotalCardRefunds = 0m,
            ExpectedCashInDrawer = 250m,
            CountedCashAmount = 245m, // -5 cash shortage
            VarianceAmount = -5m,
            ExpectedCardAmount = 400m,
            CountedCardAmount = 400m,
            CardVarianceAmount = 0m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _zReportRepo.Setup(r => r.GetNextZSequenceNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var shiftOrders = new List<PosOrderHeader>
        {
            new()
            {
                Id = 1,
                ShiftId = 10,
                BranchId = 1,
                InvoiceNumber = "INV-001",
                IsPaid = true,
                SubtotalAmount = 710m,
                TotalAmount = 710m,
                CreationDate = DateTime.UtcNow,
                Payments = new List<PosOrderPayment>
                {
                    new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 200m },
                    new() { PaymentMethod = PosPaymentMethod.Card, Amount = 400m },
                    new() { PaymentMethod = PosPaymentMethod.CustomerAccount, Amount = 80m },
                    new() { PaymentMethod = PosPaymentMethod.Hospitality, Amount = 30m }
                }
            }
        };

        _orderRepo.Setup(r => r.GetOrdersForZReportAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(shiftOrders);

        PosZReport? savedReport = null;
        _zReportRepo.Setup(r => r.AddZReportAsync(It.IsAny<PosZReport>(), It.IsAny<CancellationToken>()))
            .Callback<PosZReport, CancellationToken>((z, _) => { z.Id = 999; savedReport = z; })
            .Returns(Task.CompletedTask);
        _zReportRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _zReportRepo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedReport);

        var dto = new GenerateZReportDto { BranchId = 1, ShiftId = 10 };

        // Act
        var result = await _zReportService.GenerateZReportAsync(dto, "admin");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var z = result.Data;
        Assert.Equal(200m, z.CashPaymentsTotal);
        Assert.Equal(400m, z.CardPaymentsTotal);
        Assert.Equal(80m, z.CustomerAccountPaymentsTotal);
        Assert.Equal(30m, z.HospitalityPaymentsTotal);

        Assert.Equal(250m, z.ExpectedDrawerCash);
        Assert.Equal(245m, z.ActualCountedCash);
        Assert.Equal(-5m, z.CashVariance);

        Assert.Equal(400m, z.ExpectedCardAmount);
        Assert.Equal(400m, z.ActualCountedCard);
        Assert.Equal(0m, z.CardVariance);

        // Check tender reconciliations
        var recs = z.TenderReconciliations;
        Assert.Equal(4, recs.Count);

        var custRec = recs.First(r => r.PaymentMethod == (int)PosPaymentMethod.CustomerAccount);
        Assert.Equal(80m, custRec.ExpectedAmount);
        Assert.Equal(80m, custRec.CountedAmount);
        Assert.True(custRec.IsAutoDeclared);

        var hospRec = recs.First(r => r.PaymentMethod == (int)PosPaymentMethod.Hospitality);
        Assert.Equal(30m, hospRec.ExpectedAmount);
        Assert.Equal(30m, hospRec.CountedAmount);
        Assert.True(hospRec.IsAutoDeclared);
    }

    [Fact]
    public async Task CreateVoucherAsync_CashPaymentVoucher_IncreasesPayOut_AndRecalculatesExpectedCash()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 5,
            BranchId = 1,
            TillId = 2,
            ShiftNumber = "SH-005",
            Status = PosShiftStatus.Open,
            OpeningFloat = 100m,
            TotalCashSales = 400m,
            TotalPayOut = 0m,
            ExpectedCashInDrawer = 500m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.AddCashMovementAsync(It.IsAny<PosShiftCashMovement>(), It.IsAny<CancellationToken>()))
            .Returns<PosShiftCashMovement, CancellationToken>((m, _) => { m.Id = 101; return Task.FromResult(m); });
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var voucherDto = new CreatePosVoucherDto
        {
            ShiftId = 5,
            VoucherType = (int)PosVoucherType.Payment, // صرف
            PaymentMethod = (int)PosPaymentMethod.Cash, // نقدي
            Amount = 45m,
            GlAccountCode = "532101",
            BeneficiaryName = "مكتبة النور للقرطاسية",
            TaxInvoiceNumber = "INV-2026-99",
            Reason = "شراء ورق وطابعات وأقلام للكاشير",
            ApprovedBy = "مشرف الوردية"
        };

        // Act
        var result = await _shiftService.CreateVoucherAsync(voucherDto, "cashier1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(101, result.Data.Id);
        Assert.Equal("PV-SH5-0001", result.Data.VoucherNumber);
        Assert.Equal(45m, result.Data.Amount);
        Assert.Equal((int)PosPaymentMethod.Cash, result.Data.PaymentMethod);
        Assert.Equal((int)PosVoucherType.Payment, result.Data.VoucherType);
        Assert.Equal("سند صرف", result.Data.VoucherTypeName);

        // Shift Drawer should now reflect the payout
        Assert.Equal(45m, shift.TotalPayOut);
        Assert.Equal(455m, shift.ExpectedCashInDrawer); // 500 - 45 = 455
    }

    [Fact]
    public async Task CreateVoucherAsync_CardReceiptVoucher_IncreasesCardSales_AndAssignsCorrectNumber()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 8,
            BranchId = 1,
            TillId = 2,
            ShiftNumber = "SH-008",
            Status = PosShiftStatus.Open,
            OpeningFloat = 50m,
            TotalCardSales = 200m,
            ExpectedCardAmount = 200m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.AddCashMovementAsync(It.IsAny<PosShiftCashMovement>(), It.IsAny<CancellationToken>()))
            .Returns<PosShiftCashMovement, CancellationToken>((m, _) => { m.Id = 202; return Task.FromResult(m); });
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var voucherDto = new CreatePosVoucherDto
        {
            ShiftId = 8,
            VoucherType = (int)PosVoucherType.Receipt, // قبض
            PaymentMethod = (int)PosPaymentMethod.Card, // شبكة / مدى
            Amount = 150m,
            GlAccountCode = "411101",
            BeneficiaryName = "شركة الواحة للمناسبات",
            Reason = "عربون حجز قاعة بالبطاقة",
            ApprovedBy = "مدير الفرع"
        };

        // Act
        var result = await _shiftService.CreateVoucherAsync(voucherDto, "cashier2");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(202, result.Data.Id);
        Assert.Equal("RV-SH8-0001", result.Data.VoucherNumber);
        Assert.Equal(150m, result.Data.Amount);
        Assert.Equal((int)PosPaymentMethod.Card, result.Data.PaymentMethod);
        Assert.Equal((int)PosVoucherType.Receipt, result.Data.VoucherType);
        Assert.Equal("سند قبض", result.Data.VoucherTypeName);

        // Card totals updated
        Assert.Equal(350m, shift.TotalCardSales); // 200 + 150 = 350
        Assert.Equal(350m, shift.ExpectedCardAmount);
    }

    [Fact]
    public async Task GenerateXReportAsync_ReturnsAccurateLiveSnapshot_WithoutPersistingOrClosingShift()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 15,
            BranchId = 1,
            TillId = 3,
            ShiftNumber = "SH-015",
            Status = PosShiftStatus.Open,
            OpeningFloat = 120m,
            TotalCashSales = 350m,
            TotalCardSales = 600m,
            TotalFloatIn = 50m,
            TotalPayOut = 20m,
            ExpectedCashInDrawer = 500m,
            CountedCashAmount = null // Not closed yet
        };

        var orders = new List<PosOrderHeader>
        {
            new PosOrderHeader
            {
                Id = 1,
                BranchId = 1,
                ShiftId = 15,
                OrderType = PosOrderType.DineIn,
                InvoiceNumber = "INV-001",
                SubtotalAmount = 200m,
                DiscountAmount = 10m,
                TaxAmount = 28.5m,
                TotalAmount = 218.5m,
                Covers = 4,
                CreationDate = DateTime.UtcNow.AddHours(-2),
                Lines = new List<PosOrderLine>
                {
                    new PosOrderLine { ItemName = "وجبة برجر", Quantity = 2, UnitPrice = 80m, PrepStation = "المطبخ الرئيسي" },
                    new PosOrderLine { ItemName = "عصير برتقال", Quantity = 2, UnitPrice = 20m, PrepStation = "المشروبات" }
                },
                Payments = new List<PosOrderPayment> { new PosOrderPayment { PaymentMethod = PosPaymentMethod.Cash, Amount = 218.5m } }
            },
            new PosOrderHeader
            {
                Id = 2,
                BranchId = 1,
                ShiftId = 15,
                OrderType = PosOrderType.Takeaway,
                InvoiceNumber = "INV-002",
                SubtotalAmount = 150m,
                DiscountAmount = 0m,
                TaxAmount = 22.5m,
                TotalAmount = 172.5m,
                Covers = 1,
                CreationDate = DateTime.UtcNow.AddHours(-1),
                Lines = new List<PosOrderLine>
                {
                    new PosOrderLine { ItemName = "وجبة شاورما", Quantity = 3, UnitPrice = 50m, PrepStation = "المطبخ الرئيسي" }
                },
                Payments = new List<PosOrderPayment> { new PosOrderPayment { PaymentMethod = PosPaymentMethod.Card, Amount = 172.5m } }
            }
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _orderRepo.Setup(r => r.GetOrdersForZReportAsync(1, 15, It.IsAny<CancellationToken>())).ReturnsAsync(orders);

        // Act
        var result = await _zReportService.GenerateXReportAsync(1, 15, null, "cashier_alice");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var x = result.Data;
        Assert.Equal("X-REPORT", x.ReportType);
        Assert.True(x.IsLiveSnapshot);
        Assert.Equal(15, x.ShiftId);
        Assert.Equal("SH-015", x.ShiftNumber);
        Assert.Equal((int)PosShiftStatus.Open, x.ShiftStatus);
        Assert.Equal(350m, x.GrossSalesAmount); // 200 + 150 = 350
        Assert.Equal(10m, x.TotalDiscountAmount);
        Assert.Equal(340m, x.NetSalesAmount);
        Assert.Equal(51m, x.TotalTaxAmount); // 28.5 + 22.5 = 51

        // Order types
        Assert.Equal(2, x.OrderTypeBreakdowns.Count);
        var dineIn = x.OrderTypeBreakdowns.First(b => b.OrderType == (int)PosOrderType.DineIn);
        Assert.Equal(1, dineIn.OrdersCount);
        Assert.Equal(218.5m, dineIn.TotalAmount);

        // Operational stats
        Assert.NotNull(x.OperationalStats);
        Assert.Equal(2, x.OperationalStats.TotalInvoiceCount);
        Assert.Equal(5, x.OperationalStats.TotalGuestsCovers);
        Assert.Equal(170m, x.OperationalStats.AverageTicketAmount); // 340 / 2 = 170
        Assert.Equal(68m, x.OperationalStats.AverageSpendPerGuest); // 340 / 5 = 68
        Assert.Equal("INV-001", x.OperationalStats.FirstInvoiceNumber);
        Assert.Equal("INV-002", x.OperationalStats.LastInvoiceNumber);

        // Category sales breakdown
        Assert.NotEmpty(x.CategorySales);

        // Shift was NOT modified or saved
        _shiftRepo.Verify(r => r.UpdateShiftAsync(It.IsAny<PosShift>(), It.IsAny<CancellationToken>()), Times.Never);
        _zReportRepo.Verify(r => r.AddZReportAsync(It.IsAny<PosZReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseShift_AutomaticallyGenerates_AndAttaches_ZReport()
    {
        // Arrange
        var mockZReportService = new Mock<IPosZReportService>();
        var shiftServiceWithZ = new PosShiftService(_shiftRepo.Object, _auditService.Object, null, null, mockZReportService.Object);

        var shift = new PosShift
        {
            Id = 20,
            BranchId = 1,
            TillId = 3,
            ShiftNumber = "SH-020",
            Status = PosShiftStatus.Open,
            OpeningFloat = 100m,
            TotalCashSales = 500m,
            ExpectedCashInDrawer = 600m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.HasParkedOrdersAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var expectedZReport = new ZReportSummaryDto
        {
            Id = 99,
            BranchId = 1,
            ZSequenceNumber = 12,
            NetSalesAmount = 500m,
            CashPaymentsTotal = 500m
        };

        mockZReportService
            .Setup(z => z.GenerateZReportAsync(It.Is<GenerateZReportDto>(d => d.ShiftId == 20 && d.BranchId == 1), "cashier_sam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ZReportSummaryDto>.CreateSuccess(expectedZReport));

        mockZReportService
            .Setup(z => z.GetZReportByShiftIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ZReportSummaryDto>.CreateSuccess(expectedZReport));

        var closeDto = new BlindCloseShiftDto
        {
            ShiftId = 20,
            CountedCashAmount = 600m
        };

        // Act
        var result = await shiftServiceWithZ.BlindCloseShiftAsync(closeDto, "cashier_sam");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(PosShiftStatus.BlindClosed, result.Data.Status);
        Assert.NotNull(result.Data.ZReport);
        Assert.Equal(12, result.Data.ZReport.ZSequenceNumber);
        Assert.Equal(500m, result.Data.ZReport.NetSalesAmount);

        mockZReportService.Verify(z => z.GenerateZReportAsync(It.Is<GenerateZReportDto>(d => d.ShiftId == 20), "cashier_sam", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReceiptVoucherAsync_CreatesReceiptMovement_AndIncreasesFloatIn()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 50,
            BranchId = 1,
            TillId = 2,
            ShiftNumber = "SH-050",
            Status = PosShiftStatus.Open,
            OpeningFloat = 100m,
            ExpectedCashInDrawer = 100m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.AddCashMovementAsync(It.IsAny<PosShiftCashMovement>(), It.IsAny<CancellationToken>()))
            .Callback<PosShiftCashMovement, CancellationToken>((m, _) => shift.CashMovements.Add(m))
            .Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var receiptDto = new CreatePosReceiptVoucherDto
        {
            ShiftId = 50,
            PaymentMethod = (int)PosPaymentMethod.Cash,
            Amount = 150m,
            PayerName = "Customer Ali",
            ReferenceNumber = "REC-REF-01",
            GlAccountCode = "112101",
            Reason = "Customer Account Debt Payment",
            ApprovedBy = "Supervisor"
        };

        // Act
        var result = await _shiftService.CreateReceiptVoucherAsync(receiptDto, "cashier_ali");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal((int)PosVoucherType.Receipt, result.Data.VoucherType);
        Assert.Equal("سند قبض", result.Data.VoucherTypeName);
        Assert.Equal(150m, result.Data.Amount);
        Assert.Equal("Customer Ali", result.Data.BeneficiaryName);
        Assert.Equal("REC-REF-01", result.Data.TaxInvoiceNumber);
        Assert.Equal(150m, shift.TotalFloatIn);
        Assert.Equal(250m, shift.ExpectedCashInDrawer);

        // Verify GetShiftReceiptsAsync returns the receipt
        var receipts = await _shiftService.GetShiftReceiptsAsync(50);
        Assert.True(receipts.Success);
        Assert.Single(receipts.Data!);
        Assert.Equal("Customer Ali", receipts.Data![0].BeneficiaryName);
    }

    [Fact]
    public async Task CreatePaymentVoucherAsync_CreatesPaymentMovement_AndIncreasesPayOut()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 51,
            BranchId = 1,
            TillId = 2,
            ShiftNumber = "SH-051",
            Status = PosShiftStatus.Open,
            OpeningFloat = 200m,
            ExpectedCashInDrawer = 200m
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(51, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.AddCashMovementAsync(It.IsAny<PosShiftCashMovement>(), It.IsAny<CancellationToken>()))
            .Callback<PosShiftCashMovement, CancellationToken>((m, _) => shift.CashMovements.Add(m))
            .Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var paymentDto = new CreatePosPaymentVoucherDto
        {
            ShiftId = 51,
            PaymentMethod = (int)PosPaymentMethod.Cash,
            Amount = 35m,
            BeneficiaryName = "Cleaning Services Co",
            TaxInvoiceNumber = "TAX-INV-777",
            GlAccountCode = "523101",
            Reason = "Branch Cleaning Supplies",
            ApprovedBy = "Branch Manager"
        };

        // Act
        var result = await _shiftService.CreatePaymentVoucherAsync(paymentDto, "cashier_ali");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal((int)PosVoucherType.Payment, result.Data.VoucherType);
        Assert.Equal("سند صرف", result.Data.VoucherTypeName);
        Assert.Equal(35m, result.Data.Amount);
        Assert.Equal("Cleaning Services Co", result.Data.BeneficiaryName);
        Assert.Equal("TAX-INV-777", result.Data.TaxInvoiceNumber);
        Assert.Equal(35m, shift.TotalPayOut);
        Assert.Equal(165m, shift.ExpectedCashInDrawer);

        // Verify GetShiftPaymentsAsync returns the payment
        var payments = await _shiftService.GetShiftPaymentsAsync(51);
        Assert.True(payments.Success);
        Assert.Single(payments.Data!);
        Assert.Equal("Cleaning Services Co", payments.Data![0].BeneficiaryName);
    }

    [Fact]
    public async Task GetReceiptAndExpenseAccountsLookup_ReturnsConfiguredAccounts()
    {
        // Act
        var receipts = await _shiftService.GetReceiptAccountsLookupAsync();
        var expenses = await _shiftService.GetExpenseAccountsLookupAsync();

        // Assert
        Assert.True(receipts.Success);
        Assert.NotEmpty(receipts.Data!);
        Assert.Contains(receipts.Data!, a => a.AccountCode == "112101");

        Assert.True(expenses.Success);
        Assert.NotEmpty(expenses.Data!);
        Assert.Contains(expenses.Data!, a => a.AccountCode == "523101");
    }

    [Fact]
    public async Task GetDefaultAccountsAsync_WithBranchId_ReturnsBranchSpecificAccounts()
    {
        // Arrange
        var branchSetting = new PosBranchSetting
        {
            Id = 1,
            BranchId = 2,
            DefaultCashAccount = "111103",
            DefaultExpenseAccount = "525102",
            DefaultReceiptAccount = "421102"
        };
        _branchSettingRepo.Setup(r => r.GetByBranchIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(branchSetting);

        // Act
        var result = await _shiftService.GetDefaultAccountsAsync(2);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.BranchId);
        Assert.Equal("111103", result.Data.DefaultCashAccount);
        Assert.Equal("525102", result.Data.DefaultExpenseAccount);
        Assert.Equal("421102", result.Data.DefaultReceiptAccount);
    }

    [Fact]
    public async Task UpdateDefaultAccountsAsync_WithBranchId_AddsOrUpdatesBranchSetting()
    {
        // Arrange
        PosBranchSetting? capturedSetting = null;
        _branchSettingRepo.Setup(r => r.GetByBranchIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((PosBranchSetting?)null);
        _branchSettingRepo.Setup(r => r.AddAsync(It.IsAny<PosBranchSetting>(), It.IsAny<CancellationToken>()))
            .Callback<PosBranchSetting, CancellationToken>((s, _) => capturedSetting = s)
            .Returns(Task.CompletedTask);
        _branchSettingRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var updateDto = new UpdatePosDefaultAccountsDto
        {
            BranchId = 3,
            DefaultCashAccount = "111105",
            DefaultExpenseAccount = "525105",
            DefaultReceiptAccount = "421105"
        };

        // Act
        var result = await _shiftService.UpdateDefaultAccountsAsync(updateDto, "admin_user");

        // Assert
        Assert.NotNull(capturedSetting);
        Assert.Equal(3, capturedSetting.BranchId);
        Assert.Equal("111105", capturedSetting.DefaultCashAccount);
        Assert.Equal("525105", capturedSetting.DefaultExpenseAccount);
        Assert.Equal("421105", capturedSetting.DefaultReceiptAccount);
        _branchSettingRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentVoucher_UsesBranchSpecificExpenseAccount_WhenNotSpecifiedInDto()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 60,
            BranchId = 5,
            TillId = 3,
            ShiftNumber = "SH-060",
            Status = PosShiftStatus.Open,
            OpeningFloat = 500m,
            ExpectedCashInDrawer = 500m
        };

        var branchSetting = new PosBranchSetting
        {
            BranchId = 5,
            DefaultCashAccount = "111107",
            DefaultExpenseAccount = "525199",
            DefaultReceiptAccount = "421199"
        };

        _shiftRepo.Setup(r => r.GetShiftByIdAsync(60, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.AddCashMovementAsync(It.IsAny<PosShiftCashMovement>(), It.IsAny<CancellationToken>()))
            .Callback<PosShiftCashMovement, CancellationToken>((m, _) => shift.CashMovements.Add(m))
            .Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _branchSettingRepo.Setup(r => r.GetByBranchIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(branchSetting);

        var paymentDto = new CreatePosPaymentVoucherDto
        {
            ShiftId = 60,
            PaymentMethod = (int)PosPaymentMethod.Cash,
            Amount = 50m,
            BeneficiaryName = "Coffee Vendor",
            Reason = "Branch 5 Hospitality"
        };

        // Act
        var result = await _shiftService.CreatePaymentVoucherAsync(paymentDto, "cashier_b5");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("525199", result.Data.GlAccountCode);
        Assert.Single(shift.CashMovements);
        Assert.Equal("525199", shift.CashMovements.First().GlAccountCode);
    }
}
