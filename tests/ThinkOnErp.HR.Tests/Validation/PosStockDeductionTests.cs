using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class PosStockDeductionTests
{
    private readonly Mock<IPosOrderRepository> _orderRepo = new();
    private readonly Mock<IPosShiftRepository> _shiftRepo = new();
    private readonly Mock<IPosTableRepository> _tableRepo = new();
    private readonly Mock<IPosCalculationEngine> _calcEngine = new();
    private readonly Mock<IPosAuditService> _auditService = new();
    private readonly Mock<IPosModifierRepository> _modifierRepo = new();
    private readonly Mock<IInvoiceBusinessValidationService> _validationService = new();
    private readonly Mock<IDynamicValidationEngine> _dynamicValidationEngine = new();
    private readonly Mock<IInvStockLedgerService> _stockLedgerService = new();
    private readonly Mock<ISysSettingRepository> _settingRepo = new();
    private readonly Mock<IInvWarehouseRepository> _warehouseRepo = new();
    private readonly Mock<ILogger<PosOrderService>> _orderLogger = new();

    private readonly Mock<IPosZReportRepository> _zReportRepo = new();
    private readonly Mock<ILogger<PosZReportService>> _zLogger = new();

    private readonly PosOrderService _posOrderService;
    private readonly PosZReportService _posZReportService;

    public PosStockDeductionTests()
    {
        _posOrderService = new PosOrderService(
            _orderRepo.Object,
            _shiftRepo.Object,
            _tableRepo.Object,
            _calcEngine.Object,
            _auditService.Object,
            _modifierRepo.Object,
            _validationService.Object,
            _dynamicValidationEngine.Object,
            _stockLedgerService.Object,
            _settingRepo.Object,
            _warehouseRepo.Object,
            _orderLogger.Object);

        _posZReportService = new PosZReportService(
            _zReportRepo.Object,
            _shiftRepo.Object,
            _orderRepo.Object,
            _auditService.Object,
            _stockLedgerService.Object,
            _settingRepo.Object,
            _warehouseRepo.Object,
            _zLogger.Object);

        // Default dynamic validation pass
        _dynamicValidationEngine
            .Setup(x => x.Validate(It.IsAny<CreatePosOrderDto>(), It.IsAny<string?>(), It.IsAny<long?>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _validationService
            .Setup(x => x.ValidatePosOrderDtoAsync(It.IsAny<CreatePosOrderDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessValidationResult());

        _modifierRepo
            .Setup(x => x.GetGroupsByItemIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PosModifierGroup>());

        _calcEngine
            .Setup(x => x.CalculateOrderTotalsAsync(It.IsAny<long>(), It.IsAny<CreatePosOrderDto>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long branchId, CreatePosOrderDto dto, decimal? tax, CancellationToken ct) =>
            {
                var total = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
                return new PosOrderCalculationResult
                {
                    SubtotalAmount = total,
                    TotalAmount = total,
                    Lines = dto.Lines.Select(l => new CalculatedLineResult
                    {
                        LineNumber = l.LineNumber,
                        ItemId = l.ItemId,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineTotal = l.Quantity * l.UnitPrice,
                        TaxPercent = 15m
                    }).ToList()
                };
            });

        _stockLedgerService
            .Setup(x => x.PostMovementAsync(It.IsAny<StockMovementRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<StockMovementDto>.CreateSuccess(new StockMovementDto()));
    }

    [Fact]
    public async Task CreateOrderAsync_WhenModeIsRealTime_DeductsStockImmediately()
    {
        // Arrange
        // Setting 50 = "1" (RealTime) referencing SYS_CODE
        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode))
            .ReturnsAsync(new SysSetting
            {
                SettingCode = SysSettingKeys.PosStockDeductionMode,
                SettingValue = "1"
            });

        // Setting 51 = "61" (Default POS Warehouse)
        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId))
            .ReturnsAsync(new SysSetting
            {
                SettingCode = SysSettingKeys.PosDefaultWarehouseId,
                SettingValue = "61"
            });

        _shiftRepo
            .Setup(x => x.GetShiftByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, Status = PosShiftStatus.Open });

        _orderRepo
            .Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) => new PosOrderHeader
            {
                Id = id,
                BranchId = 1,
                IsPaid = true,
                TotalAmount = 50m,
                PaidAmount = 50m
            });

        var createDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 101, Quantity = 3, UnitPrice = 10m, UomId = 1 },
                new() { LineNumber = 2, ItemId = 102, Quantity = 2, UnitPrice = 10m, UomId = 1 }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 50m, TenderedAmount = 50m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(createDto, "testuser");

        // Assert
        Assert.True(result.Success);
        // Verify PostMovementAsync was called twice (once for Item 101, once for Item 102)
        _stockLedgerService.Verify(x => x.PostMovementAsync(
            It.Is<StockMovementRequestDto>(m => m.ItemId == 101 && m.Quantity == 3 && m.WarehouseId == 61 && m.TransactionType == SysCodeKeys.StockTransactionTypes.PosSales),
            It.IsAny<CancellationToken>()), Times.Once);

        _stockLedgerService.Verify(x => x.PostMovementAsync(
            It.Is<StockMovementRequestDto>(m => m.ItemId == 102 && m.Quantity == 2 && m.WarehouseId == 61 && m.TransactionType == SysCodeKeys.StockTransactionTypes.PosSales),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenModeIsNone_DoesNotDeductStock()
    {
        // Arrange
        // Setting 50 = "3" (None) referencing SYS_CODE
        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode))
            .ReturnsAsync(new SysSetting
            {
                SettingCode = SysSettingKeys.PosStockDeductionMode,
                SettingValue = "3"
            });

        _shiftRepo
            .Setup(x => x.GetShiftByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, Status = PosShiftStatus.Open });

        _orderRepo
            .Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) => new PosOrderHeader
            {
                Id = id,
                BranchId = 1,
                IsPaid = true,
                TotalAmount = 30m,
                PaidAmount = 30m
            });

        var createDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 101, Quantity = 3, UnitPrice = 10m, UomId = 1 }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 30m, TenderedAmount = 30m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(createDto, "testuser");

        // Assert
        Assert.True(result.Success);
        // Verify PostMovementAsync was NEVER called
        _stockLedgerService.Verify(x => x.PostMovementAsync(It.IsAny<StockMovementRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundOrderAsync_WhenModeIsRealTime_RestocksItemsImmediately()
    {
        // Arrange
        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode))
            .ReturnsAsync(new SysSetting { SettingCode = 50, SettingValue = "1" });

        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId))
            .ReturnsAsync(new SysSetting { SettingCode = 51, SettingValue = "61" });

        var origOrder = new PosOrderHeader
        {
            Id = 55,
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            IsPaid = true,
            Lines = new List<PosOrderLine>
            {
                new() { Id = 10, LineNumber = 1, ItemId = 201, Quantity = 5, UnitPrice = 20m, UomId = 1 }
            }
        };

        _orderRepo
            .Setup(x => x.GetOrderByIdAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(origOrder);

        _orderRepo
            .Setup(x => x.GetOrderByIdAsync(It.IsNotIn(55L), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosOrderHeader { Id = 99, IsPaid = true, BranchId = 1 });

        var refundDto = new RefundOrderDto
        {
            OrderId = 55,
            RefundReason = "Damaged packaging",
            RefundLines = new List<RefundOrderLineDto>
            {
                new() { OrderLineId = 10, QuantityToRefund = 2 }
            }
        };

        // Act
        var result = await _posOrderService.RefundOrderAsync(refundDto, "cashier1");

        // Assert
        Assert.True(result.Success);
        // Verify PostMovementAsync was called for restock (TransactionType = 1501, Quantity = 2)
        _stockLedgerService.Verify(x => x.PostMovementAsync(
            It.Is<StockMovementRequestDto>(m => m.ItemId == 201 && m.Quantity == 2 && m.WarehouseId == 61 && m.TransactionType == SysCodeKeys.StockTransactionTypes.SalesReturnRestock),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateZReportAsync_WhenModeIsConsolidated_AggregatesAndDeductsStock()
    {
        // Arrange
        // Setting 50 = "2" (Consolidated)
        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode))
            .ReturnsAsync(new SysSetting { SettingCode = 50, SettingValue = "2" });

        _settingRepo
            .Setup(x => x.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId))
            .ReturnsAsync(new SysSetting { SettingCode = 51, SettingValue = "61" });

        var shiftOrders = new List<PosOrderHeader>
        {
            new()
            {
                Id = 1,
                ShiftId = 10,
                IsPaid = true,
                IsRefund = false,
                Lines = new List<PosOrderLine>
                {
                    new() { ItemId = 301, Quantity = 4, UomId = 1 },
                    new() { ItemId = 302, Quantity = 2, UomId = 1 }
                }
            },
            new()
            {
                Id = 2,
                ShiftId = 10,
                IsPaid = true,
                IsRefund = false,
                Lines = new List<PosOrderLine>
                {
                    new() { ItemId = 301, Quantity = 6, UomId = 1 } // Total 301 should be 4 + 6 = 10
                }
            }
        };

        _orderRepo
            .Setup(x => x.GetActiveOrdersAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shiftOrders);

        _shiftRepo
            .Setup(x => x.GetShiftByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 10, BranchId = 1, Status = PosShiftStatus.Open });

        _zReportRepo
            .Setup(x => x.GetNextZSequenceNumberAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(105);

        _zReportRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosZReport { Id = 1, ZSequenceNumber = 105, BranchId = 1 });

        var zDto = new GenerateZReportDto
        {
            BranchId = 1,
            ShiftId = 10
        };

        // Act
        var result = await _posZReportService.GenerateZReportAsync(zDto, "manager1");

        // Assert
        Assert.True(result.Success);
        // Verify aggregated movement for Item 301 (total = 10)
        _stockLedgerService.Verify(x => x.PostMovementAsync(
            It.Is<StockMovementRequestDto>(m => m.ItemId == 301 && m.Quantity == 10 && m.WarehouseId == 61 && m.TransactionType == SysCodeKeys.StockTransactionTypes.PosSales),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify aggregated movement for Item 302 (total = 2)
        _stockLedgerService.Verify(x => x.PostMovementAsync(
            It.Is<StockMovementRequestDto>(m => m.ItemId == 302 && m.Quantity == 2 && m.WarehouseId == 61 && m.TransactionType == SysCodeKeys.StockTransactionTypes.PosSales),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_And_RefundOrder_ShouldReflectExactStockBalanceBeforeAndAfter()
    {
        // Arrange
        // Stock deduction mode: RealTime (1)
        _settingRepo.Setup(x => x.GetByCodeAsync(SysSettingKeys.PosStockDeductionMode))
            .ReturnsAsync(new SysSetting { SettingCode = SysSettingKeys.PosStockDeductionMode, SettingValue = "1" });

        _settingRepo.Setup(x => x.GetByCodeAsync(SysSettingKeys.PosDefaultWarehouseId))
            .ReturnsAsync(new SysSetting { SettingCode = SysSettingKeys.PosDefaultWarehouseId, SettingValue = "61" });

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, Status = PosShiftStatus.Open });

        decimal simulatedStock = 100m; // Stock BEFORE invoice
        decimal stockBeforeInvoice = simulatedStock;

        // Mock PostMovementAsync to dynamically adjust simulatedStock
        _stockLedgerService.Setup(x => x.PostMovementAsync(It.IsAny<StockMovementRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockMovementRequestDto req, CancellationToken ct) =>
            {
                if (req.TransactionType == SysCodeKeys.StockTransactionTypes.PosSales)
                {
                    simulatedStock -= req.Quantity; // Deduct sales
                }
                else if (req.TransactionType == SysCodeKeys.StockTransactionTypes.SalesReturnRestock)
                {
                    simulatedStock += req.Quantity; // Restock refund
                }
                return ApiResponse<StockMovementDto>.CreateSuccess(new StockMovementDto());
            });

        _orderRepo.Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) => new PosOrderHeader
            {
                Id = id == 0 ? 10 : id,
                BranchId = 1,
                ShiftId = 1,
                IsPaid = true,
                TotalAmount = 50m,
                PaidAmount = 50m,
                Lines = new List<PosOrderLine>
                {
                    new() { Id = 101, LineNumber = 1, ItemId = 500, Quantity = 5, UnitPrice = 10m, UomId = 1 }
                }
            });

        var createDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 500, Quantity = 5, UnitPrice = 10m, UomId = 1 }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 50m, TenderedAmount = 50m }
            }
        };

        // Act 1: Create Order & Pay (Invoice)
        var orderResult = await _posOrderService.CreateOrderAsync(createDto, "cashier");

        // Assert 1: Stock AFTER Invoice
        Assert.True(orderResult.Success);
        Assert.Equal(100m, stockBeforeInvoice);
        Assert.Equal(95m, simulatedStock); // 100 - 5 = 95

        // Act 2: Refund 2 items
        var refundDto = new RefundOrderDto
        {
            OrderId = 10,
            RefundReason = "Customer changed mind",
            RefundLines = new List<RefundOrderLineDto>
            {
                new() { OrderLineId = 101, QuantityToRefund = 2 }
            }
        };

        var refundResult = await _posOrderService.RefundOrderAsync(refundDto, "cashier");

        // Assert 2: Stock AFTER Refund
        Assert.True(refundResult.Success);
        Assert.Equal(97m, simulatedStock); // 95 + 2 = 97
    }

    [Fact]
    public async Task CreateOrder_ShouldFail_WhenModifiersExceedMaxSelections()
    {
        // Arrange
        _shiftRepo.Setup(x => x.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, Status = PosShiftStatus.Open });

        // Item 700 has a modifier group with MaxSelections = 1
        var modGroup = new PosModifierGroup
        {
            Id = 5,
            GroupNameLocal = "إضافات الصوص",
            MaxSelections = 1,
            IsActive = true,
            Options = new List<InvModifierOption>
            {
                new() { Id = 51, ModifierGroupId = 5, OptionNameLocal = "مايونيز", IsActive = true },
                new() { Id = 52, ModifierGroupId = 5, OptionNameLocal = "كاتشب", IsActive = true }
            }
        };

        _modifierRepo.Setup(m => m.GetGroupsByItemIdAsync(700, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PosModifierGroup> { modGroup });

        var createDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new()
                {
                    LineNumber = 1,
                    ItemId = 700,
                    ItemName = "برجر دجاج",
                    Quantity = 1,
                    UnitPrice = 25m,
                    Modifiers = new List<CreatePosOrderLineModifierDto>
                    {
                        new() { ModifierItemId = 51, ModifierName = "مايونيز", Quantity = 1 },
                        new() { ModifierItemId = 52, ModifierName = "كاتشب", Quantity = 1 } // 2 selections > MaxSelections 1
                    }
                }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(createDto, "cashier");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("exceeds maximum allowed selections", result.Message);
    }
}
