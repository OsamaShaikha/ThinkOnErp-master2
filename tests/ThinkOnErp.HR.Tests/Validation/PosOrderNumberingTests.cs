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
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class PosOrderNumberingTests
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

    private readonly PosOrderService _posOrderService;

    public PosOrderNumberingTests()
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
                    TaxAmount = 0,
                    Lines = dto.Lines.Select(l => new CalculatedLineResult
                    {
                        LineNumber = l.LineNumber,
                        ItemId = l.ItemId,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineNetBeforeTax = l.Quantity * l.UnitPrice,
                        TaxAmount = 0,
                        TaxPercent = 0,
                        LineTotal = l.Quantity * l.UnitPrice
                    }).ToList()
                };
            });
    }

    [Fact]
    public async Task CreateOrder_ShouldAssignShiftScopedOrderNumber_AndFiscalInvoiceNumber()
    {
        // Arrange
        long branchId = 1;
        long shiftId = 55;
        int currentYear = DateTime.UtcNow.Year;

        _shiftRepo.Setup(s => s.GetShiftByIdAsync(shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = shiftId, Status = PosShiftStatus.Open });

        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(shiftId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("0001");

        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(branchId, currentYear, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"INV-{currentYear}-000008");

        PosOrderHeader? capturedOrder = null;
        _orderRepo.Setup(r => r.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Callback<PosOrderHeader, CancellationToken>((o, _) => capturedOrder = o)
            .Returns(Task.CompletedTask);

        _orderRepo.Setup(r => r.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedOrder);

        var dto = new CreatePosOrderDto
        {
            BranchId = branchId,
            ShiftId = shiftId,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 1, UnitPrice = 100, LineNumber = 1 }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "TEST_USER");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedOrder);
        Assert.Equal("0001", capturedOrder!.OrderNumber);
        Assert.Equal($"INV-{currentYear}-000008", capturedOrder.InvoiceNumber);
    }

    [Fact]
    public async Task RefundOrder_ShouldAssignRFormatOrderNumber_AndRetFormatInvoiceNumber()
    {
        // Arrange
        long branchId = 1;
        long shiftId = 55;
        int currentYear = DateTime.UtcNow.Year;

        var originalOrder = new PosOrderHeader
        {
            Id = 999,
            BranchId = branchId,
            ShiftId = shiftId,
            TillId = 1,
            IsPaid = true,
            OrderNumber = "0001",
            InvoiceNumber = $"INV-{currentYear}-000008",
            Lines = new List<PosOrderLine>
            {
                new() { Id = 1, LineNumber = 1, ItemId = 10, Quantity = 2, UnitPrice = 50, TaxAmount = 15 }
            }
        };

        _orderRepo.Setup(r => r.GetOrderByIdAsync(originalOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalOrder);

        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(shiftId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync("R0001");

        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(branchId, currentYear, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"RET-{currentYear}-000008");

        PosOrderHeader? capturedRefund = null;
        _orderRepo.Setup(r => r.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Callback<PosOrderHeader, CancellationToken>((o, _) => capturedRefund = o)
            .Returns(Task.CompletedTask);

        _orderRepo.Setup(r => r.GetOrderByIdAsync(It.Is<long>(id => id != originalOrder.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedRefund);

        var refundDto = new RefundOrderDto
        {
            OrderId = originalOrder.Id,
            RefundReason = "Customer Return",
            RefundLines = new List<RefundOrderLineDto>
            {
                new() { OrderLineId = 1, QuantityToRefund = 1 }
            }
        };

        // Act
        var result = await _posOrderService.RefundOrderAsync(refundDto, "TEST_USER");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedRefund);
        Assert.Equal("R0001", capturedRefund!.OrderNumber);
        Assert.Equal($"RET-{currentYear}-000008", capturedRefund.InvoiceNumber);
        Assert.True(capturedRefund.IsRefund);
    }
}
