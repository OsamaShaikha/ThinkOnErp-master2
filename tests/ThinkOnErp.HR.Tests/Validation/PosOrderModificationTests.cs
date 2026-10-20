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

public class PosOrderModificationTests
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

    public PosOrderModificationTests()
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

        _modifierRepo
            .Setup(x => x.GetGroupsByItemIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PosModifierGroup>());

        _calcEngine
            .Setup(x => x.CalculateOrderTotalsAsync(It.IsAny<long>(), It.IsAny<CreatePosOrderDto>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long branchId, CreatePosOrderDto dto, decimal? tax, CancellationToken ct) =>
            {
                var subtotal = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
                var taxAmt = subtotal * 0.15m;
                var total = subtotal + taxAmt;
                return new PosOrderCalculationResult
                {
                    SubtotalAmount = subtotal,
                    TaxAmount = taxAmt,
                    TotalAmount = total,
                    Lines = dto.Lines.Select(l => new CalculatedLineResult
                    {
                        LineNumber = l.LineNumber,
                        ItemId = l.ItemId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        LineNetBeforeTax = l.Quantity * l.UnitPrice,
                        TaxPercent = 15m,
                        TaxAmount = (l.Quantity * l.UnitPrice) * 0.15m,
                        LineTotal = (l.Quantity * l.UnitPrice) * 1.15m
                    }).ToList()
                };
            });
    }

    [Fact]
    public async Task AddOrderLinesAsync_AppendsSupplementaryItems_AndDispatchesOnlyNewItemsToKds()
    {
        // Arrange
        var existingOrder = new PosOrderHeader
        {
            Id = 50,
            BranchId = 1,
            ShiftId = 10,
            TableId = 5,
            OrderNumber = "ORD-050",
            OrderType = PosOrderType.DineIn,
            Status = PosOrderStatus.SentToKitchen,
            SubtotalAmount = 100m,
            TaxAmount = 15m,
            TotalAmount = 115m,
            IsPaid = false,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 101,
                    OrderId = 50,
                    LineNumber = 1,
                    ItemId = 1,
                    ItemCode = "BURGER",
                    ItemName = "وجبة برجر لحم",
                    Quantity = 2,
                    UnitPrice = 50m,
                    LineTotal = 115m,
                    KdsStatus = PosKdsStatus.Preparing, // Kitchen is currently cooking this!
                    KdsSentAt = DateTime.UtcNow.AddMinutes(-10)
                }
            }
        };

        _orderRepo.Setup(r => r.GetOrderByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(existingOrder);
        _orderRepo.Setup(r => r.UpdateOrderAsync(existingOrder, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var addDto = new AddOrderLinesDto
        {
            SendToKitchen = true,
            AdditionalNotes = "طلب إضافي عصير وحلى لطاولة 5",
            Lines = new List<CreatePosOrderLineDto>
            {
                new CreatePosOrderLineDto
                {
                    ItemId = 20,
                    ItemCode = "JUICE",
                    ItemName = "عصير برتقال طازج",
                    Quantity = 2,
                    UnitPrice = 15m,
                    PrepStation = "المشروبات",
                    SpecialInstructions = "بدون سكر"
                }
            }
        };

        // Act
        var result = await _posOrderService.AddOrderLinesAsync(50, addDto, "waiter_sam");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var updated = result.Data;
        Assert.Equal(2, updated.Lines.Count);

        // First item remained in cooking state and was NOT re-dispatched
        var firstLine = existingOrder.Lines.First(l => l.LineNumber == 1);
        Assert.Equal(PosKdsStatus.Preparing, firstLine.KdsStatus);

        // New item was appended as LineNumber 2 and sent to kitchen with status Pending
        var newLine = existingOrder.Lines.First(l => l.LineNumber == 2);
        Assert.Equal("عصير برتقال طازج", newLine.ItemName);
        Assert.Equal(2, newLine.Quantity);
        Assert.Equal(PosKdsStatus.Pending, newLine.KdsStatus);
        Assert.NotNull(newLine.KdsSentAt);
        Assert.Equal("بدون سكر", newLine.SpecialInstructions);

        // Totals recalculated: subtotal was 100 + 30 = 130
        Assert.Equal(130m, existingOrder.SubtotalAmount);
        Assert.Equal(19.5m, existingOrder.TaxAmount);
        Assert.Equal(149.5m, existingOrder.TotalAmount);
    }

    [Fact]
    public async Task UpdateOrderLineAsync_GuardsAgainstModifyingItemsAlreadyBeingCookedOrReady()
    {
        // Arrange
        var order = new PosOrderHeader
        {
            Id = 60,
            BranchId = 1,
            ShiftId = 10,
            OrderNumber = "ORD-060",
            Status = PosOrderStatus.SentToKitchen,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 201,
                    OrderId = 60,
                    ItemName = "ستيك لحم تندرلوين",
                    Quantity = 1,
                    UnitPrice = 120m,
                    KdsStatus = PosKdsStatus.Preparing // In the pan cooking!
                }
            }
        };

        _orderRepo.Setup(r => r.GetOrderByIdAsync(60, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var updateDto = new UpdateOrderLineDto
        {
            SpecialInstructions = "تغيير الاستواء إلى Medium Well"
        };

        // Act
        var result = await _posOrderService.UpdateOrderLineAsync(60, 201, updateDto, "waiter_sam");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("لا يمكن تعديل الصنف مباشرة لأن المطبخ بدأ بتحضيره بالفعل", result.Message);
    }

    [Fact]
    public async Task UpdateOrderLineAsync_AllowsModifyingPendingItem_AndRecalculatesTotals()
    {
        // Arrange
        var order = new PosOrderHeader
        {
            Id = 70,
            BranchId = 1,
            ShiftId = 10,
            OrderNumber = "ORD-070",
            Status = PosOrderStatus.SentToKitchen,
            SubtotalAmount = 80m,
            TaxAmount = 12m,
            TotalAmount = 92m,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 301,
                    OrderId = 70,
                    ItemName = "بيتزا خضار",
                    Quantity = 1,
                    UnitPrice = 80m,
                    TaxAmount = 12m,
                    LineTotal = 80m,
                    KdsStatus = PosKdsStatus.Pending // Waiting in queue!
                }
            }
        };

        _orderRepo.Setup(r => r.GetOrderByIdAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepo.Setup(r => r.UpdateOrderAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var updateDto = new UpdateOrderLineDto
        {
            SpecialInstructions = "بدون مشروم إطلاقاً (حساسية)",
            Quantity = 2
        };

        // Act
        var result = await _posOrderService.UpdateOrderLineAsync(70, 301, updateDto, "cashier_1");

        // Assert
        Assert.True(result.Success);
        var line = order.Lines.First(l => l.Id == 301);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("بدون مشروم إطلاقاً (حساسية)", line.SpecialInstructions);
        Assert.Equal(160m, order.SubtotalAmount);
    }

    [Fact]
    public async Task SplitOrderAsync_ShouldSplitItemsAndCreateChildOrder_WhenValid()
    {
        // Arrange: Table order with 2 items (Pizza 80 + Burger 40)
        var parentOrder = new PosOrderHeader
        {
            Id = 80,
            BranchId = 1,
            ShiftId = 10,
            OrderNumber = "ORD-080",
            InvoiceNumber = "INV-2026-000080",
            Status = PosOrderStatus.SentToKitchen,
            SubtotalAmount = 120m,
            TaxAmount = 18m,
            TotalAmount = 138m,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 401,
                    OrderId = 80,
                    ItemId = 101,
                    ItemCode = "PIZZA-01",
                    ItemName = "بيتزا بيبروني",
                    Quantity = 1,
                    UnitPrice = 80m,
                    TaxAmount = 12m,
                    LineTotal = 80m,
                    KdsStatus = PosKdsStatus.Pending
                },
                new PosOrderLine
                {
                    Id = 402,
                    OrderId = 80,
                    ItemId = 102,
                    ItemCode = "BURGER-01",
                    ItemName = "برجر لحم",
                    Quantity = 1,
                    UnitPrice = 40m,
                    TaxAmount = 6m,
                    LineTotal = 40m,
                    KdsStatus = PosKdsStatus.Pending
                }
            }
        };

        PosOrderHeader? addedChildOrder = null;
        _orderRepo.Setup(r => r.GetOrderByIdAsync(80, It.IsAny<CancellationToken>())).ReturnsAsync(parentOrder);
        _orderRepo.Setup(r => r.GetOrderByIdAsync(It.Is<long>(id => id != 80), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedChildOrder);
        _orderRepo.Setup(r => r.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Callback<PosOrderHeader, CancellationToken>((o, _) => { o.Id = 81; addedChildOrder = o; })
            .Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.UpdateOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(10, false, It.IsAny<CancellationToken>())).ReturnsAsync("0005");
        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(1, It.IsAny<int>(), false, It.IsAny<CancellationToken>())).ReturnsAsync("INV-2026-000081");

        _calcEngine.Setup(x => x.CalculateOrderTotalsAsync(1, It.IsAny<CreatePosOrderDto>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long branchId, CreatePosOrderDto dto, decimal? taxRate, CancellationToken ct) =>
            {
                var line = dto.Lines.First();
                return new PosOrderCalculationResult
                {
                    SubtotalAmount = line.Quantity * line.UnitPrice,
                    TaxAmount = (line.Quantity * line.UnitPrice) * 0.15m,
                    TotalAmount = (line.Quantity * line.UnitPrice) * 1.15m,
                    Lines = new List<CalculatedLineResult>
                    {
                        new CalculatedLineResult
                        {
                            LineNumber = line.LineNumber,
                            ItemId = line.ItemId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            LineNetBeforeTax = line.Quantity * line.UnitPrice,
                            TaxPercent = 15m,
                            TaxAmount = (line.Quantity * line.UnitPrice) * 0.15m,
                            LineTotal = (line.Quantity * line.UnitPrice)
                        }
                    }
                };
            });

        var splitDto = new SplitPosOrderDto
        {
            LinesToSplit = new List<SplitOrderLineItemDto>
            {
                new SplitOrderLineItemDto { LineId = 402, Quantity = 1 } // Split Burger 40
            },
            SettleImmediately = true,
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new CreatePosOrderPaymentDto
                {
                    PaymentMethod = (int)PosPaymentMethod.Cash,
                    Amount = 46m,
                    TenderedAmount = 50m
                }
            }
        };

        // Act
        var result = await _posOrderService.SplitOrderAsync(80, splitDto, "cashier_1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // Parent order should now have only Pizza (1 item remaining)
        Assert.Single(parentOrder.Lines);
        Assert.Equal(401, parentOrder.Lines.First().Id);
        Assert.Equal(80m, parentOrder.SubtotalAmount);

        // Child order should have burger and be settled
        Assert.NotNull(addedChildOrder);
        Assert.True(addedChildOrder.IsPaid);
        Assert.Equal(PosOrderStatus.Completed, addedChildOrder.Status);
        Assert.Equal(46m, addedChildOrder.TotalAmount);
    }

    [Fact]
    public async Task MergeTablesAsync_ShouldMergeTablesAndCombineCapacities_WhenValid()
    {
        // Arrange
        var tableService = new PosTableService(_tableRepo.Object, _orderRepo.Object);

        var primaryTable = new PosTable
        {
            Id = 5,
            FloorId = 1,
            TableNumber = "T5",
            Capacity = 4,
            Status = PosTableStatus.Occupied,
            ActiveOrderId = 100,
            IsActive = true
        };

        var secondaryTable = new PosTable
        {
            Id = 6,
            FloorId = 1,
            TableNumber = "T6",
            Capacity = 6,
            Status = PosTableStatus.Available,
            IsActive = true
        };

        var primaryOrder = new PosOrderHeader
        {
            Id = 100,
            BranchId = 1,
            OrderNumber = "ORD-100",
            TableId = 5,
            Status = PosOrderStatus.SentToKitchen,
            Lines = new List<PosOrderLine>()
        };

        _tableRepo.Setup(r => r.GetTableByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(primaryTable);
        _tableRepo.Setup(r => r.GetTableByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(secondaryTable);
        _orderRepo.Setup(r => r.GetOrderByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(primaryOrder);
        _tableRepo.Setup(r => r.UpdateTableAsync(It.IsAny<PosTable>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.UpdateOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tableRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mergeDto = new MergeTablesDto
        {
            PrimaryTableId = 5,
            SecondaryTableIds = new List<long> { 6 },
            Reason = "عائلة كبيرة 10 أشخاص"
        };

        // Act
        var result = await tableService.MergeTablesAsync(mergeDto, "waiter_1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.TotalCombinedCapacity); // 4 + 6
        Assert.Equal(5, secondaryTable.ParentTableId);
        Assert.Equal(100, secondaryTable.ActiveOrderId);
        Assert.Equal(PosTableStatus.Occupied, secondaryTable.Status);
    }

    [Fact]
    public async Task UnmergeTableAsync_ShouldReleaseSecondaryTable_WhenValid()
    {
        // Arrange
        var tableService = new PosTableService(_tableRepo.Object, _orderRepo.Object);

        var secondaryTable = new PosTable
        {
            Id = 6,
            FloorId = 1,
            TableNumber = "T6",
            Capacity = 6,
            Status = PosTableStatus.Occupied,
            ActiveOrderId = 100,
            ParentTableId = 5,
            IsActive = true
        };

        _tableRepo.Setup(r => r.GetTableByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(secondaryTable);
        _tableRepo.Setup(r => r.UpdateTableAsync(It.IsAny<PosTable>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tableRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await tableService.UnmergeTableAsync(6, "waiter_1");

        // Assert
        Assert.True(result.Success);
        Assert.Null(secondaryTable.ParentTableId);
        Assert.Null(secondaryTable.ActiveOrderId);
        Assert.Equal(PosTableStatus.Available, secondaryTable.Status);
    }

    [Fact]
    public async Task ExchangeOrderAsync_ShouldIssueRefundAndCreateNewSalesOrderWithPriceDifference_WhenValid()
    {
        // Arrange
        var origOrder = new PosOrderHeader
        {
            Id = 90,
            BranchId = 1,
            ShiftId = 10,
            OrderNumber = "ORD-090",
            InvoiceNumber = "INV-2026-000090",
            Status = PosOrderStatus.Completed,
            IsPaid = true,
            SubtotalAmount = 100m,
            TaxAmount = 15m,
            TotalAmount = 115m,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 901,
                    OrderId = 90,
                    ItemId = 501,
                    ItemCode = "SHIRT-M",
                    ItemName = "قميص كاجوال مقاس M",
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxAmount = 15m,
                    TaxPercent = 15m,
                    LineTotal = 100m
                }
            }
        };

        var shift = new PosShift
        {
            Id = 10,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashSales = 115m
        };

        _orderRepo.Setup(r => r.GetOrderByIdAsync(90, It.IsAny<CancellationToken>())).ReturnsAsync(origOrder);
        _shiftRepo.Setup(r => r.GetShiftByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(10, true, It.IsAny<CancellationToken>())).ReturnsAsync("0010");
        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(1, It.IsAny<int>(), true, It.IsAny<CancellationToken>())).ReturnsAsync("CN-2026-000010");
        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(10, false, It.IsAny<CancellationToken>())).ReturnsAsync("0011");
        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(1, It.IsAny<int>(), false, It.IsAny<CancellationToken>())).ReturnsAsync("INV-2026-000091");

        PosOrderHeader? createdRefund = null;
        PosOrderHeader? createdNewSales = null;

        _orderRepo.Setup(r => r.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Callback<PosOrderHeader, CancellationToken>((o, _) =>
            {
                if (o.IsRefund) { o.Id = 91; createdRefund = o; }
                else { o.Id = 92; createdNewSales = o; }
            })
            .Returns(Task.CompletedTask);

        _orderRepo.Setup(r => r.GetOrderByIdAsync(It.Is<long>(id => id != 90), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => id == 91 ? createdRefund : createdNewSales);

        _orderRepo.Setup(r => r.UpdateOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Mock Calculation Engine for New Line (Shirt L = 130 + 19.50 tax = 149.50)
        _calcEngine.Setup(x => x.CalculateOrderTotalsAsync(1, It.IsAny<CreatePosOrderDto>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long bId, CreatePosOrderDto dto, decimal? tax, CancellationToken ct) =>
            {
                var newLine = dto.Lines.First();
                var subtotal = newLine.Quantity * newLine.UnitPrice; // 130
                var taxAmt = subtotal * 0.15m; // 19.50
                return new PosOrderCalculationResult
                {
                    SubtotalAmount = subtotal,
                    TaxAmount = taxAmt,
                    TotalAmount = subtotal + taxAmt,
                    Lines = new List<CalculatedLineResult>
                    {
                        new CalculatedLineResult
                        {
                            LineNumber = newLine.LineNumber,
                            ItemId = newLine.ItemId,
                            Quantity = newLine.Quantity,
                            UnitPrice = newLine.UnitPrice,
                            LineNetBeforeTax = subtotal,
                            TaxPercent = 15m,
                            TaxAmount = taxAmt,
                            LineTotal = subtotal
                        }
                    }
                };
            });

        var exchangeDto = new ExchangeOrderDto
        {
            OriginalOrderId = 90,
            ExchangeReason = "تبديل مقاس M إلى L",
            ReturnLines = new List<RefundOrderLineDto>
            {
                new RefundOrderLineDto { OrderLineId = 901, QuantityToRefund = 1 } // Return 115m
            },
            NewLines = new List<CreatePosOrderLineDto>
            {
                new CreatePosOrderLineDto
                {
                    ItemId = 502,
                    ItemCode = "SHIRT-L",
                    ItemName = "قميص كاجوال مقاس L",
                    Quantity = 1,
                    UnitPrice = 130m // 130 + 15% = 149.50
                }
            },
            DifferencePayments = new List<CreatePosOrderPaymentDto>
            {
                new CreatePosOrderPaymentDto
                {
                    PaymentMethod = (int)PosPaymentMethod.Card,
                    Amount = 34.50m, // 149.50 - 115.00 = 34.50
                    TenderedAmount = 34.50m
                }
            }
        };

        // Act
        var result = await _posOrderService.ExchangeOrderAsync(exchangeDto, "cashier_1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(115m, result.Data.ReturnedTotalAmount);
        Assert.Equal(149.50m, result.Data.NewSalesTotalAmount);
        Assert.Equal(34.50m, result.Data.NetDifferenceAmount);
        Assert.Equal(34.50m, result.Data.PaidDifferenceAmount);
        Assert.Equal(0m, result.Data.ChangeDueToCustomer);

        Assert.NotNull(createdRefund);
        Assert.True(createdRefund.IsRefund);
        Assert.Equal(-115m, createdRefund.TotalAmount);

        Assert.NotNull(createdNewSales);
        Assert.True(createdNewSales.IsPaid);
        Assert.Equal(149.50m, createdNewSales.TotalAmount);
        Assert.Equal(2, createdNewSales.Payments.Count); // 115 exchange credit + 34.50 card
    }

    [Fact]
    public async Task RefundOrderAsync_ShouldLookupByInvoiceNumber_AndCalculateCorrectTotalsWithModifiers()
    {
        // Arrange
        var origOrder = new PosOrderHeader
        {
            Id = 350,
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            OrderNumber = "0085",
            InvoiceNumber = "INV-2026-000051",
            Status = PosOrderStatus.Completed,
            IsPaid = true,
            CustomerId = 21,
            CustomerName = "osama",
            SubtotalAmount = 7.5m,
            TaxAmount = 1.125m,
            TotalAmount = 8.625m,
            PaidAmount = 8.625m,
            Lines = new List<PosOrderLine>
            {
                new PosOrderLine
                {
                    Id = 350,
                    OrderId = 350,
                    LineNumber = 1,
                    ItemId = 81,
                    ItemCode = "ITM-90001",
                    ItemName = "Office Chair",
                    Quantity = 1.0m,
                    UomId = 1,
                    UnitPrice = 5.0m,
                    TaxAmount = 1.125m,
                    TaxPercent = 15m,
                    LineTotal = 8.625m,
                    Modifiers = new List<PosOrderLineModifier>
                    {
                        new PosOrderLineModifier
                        {
                            Id = 234,
                            ModifierItemId = 19,
                            ModifierName = "Extra Cheddar",
                            Quantity = 1.0m,
                            UnitPrice = 0m,
                            ExtraPrice = 2.5m
                        }
                    }
                }
            },
            Payments = new List<PosOrderPayment>
            {
                new PosOrderPayment
                {
                    Id = 289,
                    PaymentMethod = (PosPaymentMethod)43, // Customer Account
                    Amount = 8.625m,
                    PaymentDate = DateTime.UtcNow
                }
            }
        };

        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashSales = 100m
        };

        PosOrderHeader? createdRefund = null;

        _orderRepo.Setup(r => r.GetOrderByInvoiceNumberAsync("INV-2026-000051", It.IsAny<CancellationToken>()))
            .ReturnsAsync(origOrder);
        _orderRepo.Setup(r => r.GetOrderByIdAsync(350, It.IsAny<CancellationToken>()))
            .ReturnsAsync(origOrder);
        _shiftRepo.Setup(r => r.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        _shiftRepo.Setup(r => r.GetActiveShiftByTillAsync(41, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        _orderRepo.Setup(r => r.GenerateNextShiftOrderNumberAsync(61, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync("R0001");
        _orderRepo.Setup(r => r.GenerateNextInvoiceNumberAsync(1, It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync("RET-2026-000001");

        _orderRepo.Setup(r => r.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Callback<PosOrderHeader, CancellationToken>((o, _) =>
            {
                o.Id = 351;
                createdRefund = o;
            })
            .Returns(Task.CompletedTask);

        _orderRepo.Setup(r => r.GetOrderByIdAsync(351, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => createdRefund);
        _orderRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.UpdateShiftAsync(shift, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _shiftRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act: Refund by InvoiceNumber without OrderId, and without specifying RefundLines (full order refund)
        var refundDto = new RefundOrderDto
        {
            InvoiceNumber = "INV-2026-000051",
            RefundReason = "إرجاع كامل مع الإضافات"
        };

        var result = await _posOrderService.RefundOrderAsync(refundDto, "admin");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(-8.625m, result.Data.TotalAmount);
        Assert.Equal(-7.5m, result.Data.SubtotalAmount);
        Assert.Equal(-1.125m, result.Data.TaxAmount);
        Assert.True(result.Data.IsRefund);
        Assert.Equal(350, result.Data.OriginalOrderId);
        Assert.Equal("INV-2026-000051", result.Data.OriginalInvoiceNumber);
        Assert.Equal("0085", result.Data.OriginalOrderNumber);
        Assert.Equal("إرجاع كامل مع الإضافات", result.Data.RefundReason);

        // Verify line modifiers copied
        Assert.Single(result.Data.Lines);
        var refundLine = result.Data.Lines.First();
        Assert.Equal(-1.0m, refundLine.Quantity);
        Assert.Equal(-8.625m, refundLine.LineTotal);
        Assert.Single(refundLine.Modifiers);
        Assert.Equal("Extra Cheddar", refundLine.Modifiers.First().ModifierName);

        // Verify refund payment created
        Assert.Single(result.Data.Payments);
        Assert.Equal(-8.625m, result.Data.Payments.First().Amount);
    }
}
