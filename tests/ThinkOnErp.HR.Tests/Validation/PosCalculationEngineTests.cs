using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Application.Services.Validation;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class PosCalculationEngineTests
{
    private readonly Mock<IPosPromotionEngine> _promotionEngine = new();
    private readonly Mock<IInvoiceBusinessValidationService> _validationService = new();
    private readonly PosCalculationEngine _engine;

    public PosCalculationEngineTests()
    {
        _promotionEngine
            .Setup(p => p.EvaluatePromotionsAsync(It.IsAny<long>(), It.IsAny<List<CreatePosOrderLineDto>>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartPromotionEvaluationResult());

        _validationService
            .Setup(v => v.GetEffectiveTaxRatePercentAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(15m);

        _engine = new PosCalculationEngine(_promotionEngine.Object, _validationService.Object);
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WhenBothDiscountAmountAndPercentProvided_DoesNotDoubleDiscount()
    {
        // Arrange - User's exact case:
        // Item price 5.0, Quantity 1.0, DiscountAmount 0.5, DiscountPercent 10.0
        // Expected: Discount = 0.5 (NOT 1.0), Net = 4.5, Tax 15% = 0.675, Total = 5.175
        var orderDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 121,
            TillId = 41,
            Lines = new List<CreatePosOrderLineDto>
            {
                new()
                {
                    LineNumber = 1,
                    ItemId = 81,
                    ItemCode = "ITM-90001",
                    ItemName = "Office Chair",
                    Quantity = 1.0m,
                    UnitPrice = 5.0m,
                    DiscountAmount = 0.5m,
                    DiscountPercent = 10.0m
                }
            }
        };

        // Act
        var result = await _engine.CalculateOrderTotalsAsync(1, orderDto, defaultTaxRatePercent: 15m);

        // Assert
        Assert.Equal(5.0m, result.SubtotalAmount);
        Assert.Equal(0.5m, result.TotalDiscountAmount);
        Assert.Equal(0.5m, result.Lines[0].LineDiscount);
        Assert.Equal(4.5m, result.NetTaxableAmount);
        Assert.Equal(0.675m, result.TaxAmount);
        Assert.Equal(5.175m, result.TotalAmount);
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WhenOnlyDiscountPercentProvided_CalculatesCorrectDiscount()
    {
        // Arrange
        var orderDto = new CreatePosOrderDto
        {
            BranchId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new()
                {
                    LineNumber = 1,
                    ItemId = 81,
                    Quantity = 2.0m,
                    UnitPrice = 10.0m, // Gross = 20
                    DiscountAmount = 0m,
                    DiscountPercent = 15.0m // 15% of 20 = 3.0
                }
            }
        };

        // Act
        var result = await _engine.CalculateOrderTotalsAsync(1, orderDto, defaultTaxRatePercent: 15m);

        // Assert
        Assert.Equal(20.0m, result.SubtotalAmount);
        Assert.Equal(3.0m, result.TotalDiscountAmount);
        Assert.Equal(3.0m, result.Lines[0].LineDiscount);
        Assert.Equal(17.0m, result.NetTaxableAmount);
        Assert.Equal(2.55m, result.TaxAmount);
        Assert.Equal(19.55m, result.TotalAmount);
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WhenOnlyDiscountAmountProvided_CalculatesCorrectDiscount()
    {
        // Arrange
        var orderDto = new CreatePosOrderDto
        {
            BranchId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new()
                {
                    LineNumber = 1,
                    ItemId = 81,
                    Quantity = 1.0m,
                    UnitPrice = 50.0m,
                    DiscountAmount = 5.0m,
                    DiscountPercent = 0m
                }
            }
        };

        // Act
        var result = await _engine.CalculateOrderTotalsAsync(1, orderDto, defaultTaxRatePercent: 15m);

        // Assert
        Assert.Equal(50.0m, result.SubtotalAmount);
        Assert.Equal(5.0m, result.TotalDiscountAmount);
        Assert.Equal(45.0m, result.NetTaxableAmount);
        Assert.Equal(6.75m, result.TaxAmount);
        Assert.Equal(51.75m, result.TotalAmount);
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WhenManualOrderDiscountBothAmountAndPercentProvided_DoesNotDoubleDiscount()
    {
        // Arrange
        var orderDto = new CreatePosOrderDto
        {
            BranchId = 1,
            ManualDiscountAmount = 10.0m,
            ManualDiscountPercent = 10.0m, // Both provided
            Lines = new List<CreatePosOrderLineDto>
            {
                new()
                {
                    LineNumber = 1,
                    ItemId = 81,
                    Quantity = 1.0m,
                    UnitPrice = 100.0m
                }
            }
        };

        // Act
        var result = await _engine.CalculateOrderTotalsAsync(1, orderDto, defaultTaxRatePercent: 15m);

        // Assert
        Assert.Equal(100.0m, result.SubtotalAmount);
        Assert.Equal(10.0m, result.TotalDiscountAmount); // Must NOT be 20.0m!
        Assert.Equal(90.0m, result.NetTaxableAmount);
        Assert.Equal(13.5m, result.TaxAmount);
        Assert.Equal(103.5m, result.TotalAmount);
    }
}
