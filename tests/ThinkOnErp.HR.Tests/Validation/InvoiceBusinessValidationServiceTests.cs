using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class InvoiceBusinessValidationServiceTests
{
    private readonly Mock<IGlFiscalPeriodRepository> _fiscalRepo = new();
    private readonly Mock<ICustomerRepository> _customerRepo = new();
    private readonly Mock<IArSubledgerRepository> _arRepo = new();
    private readonly Mock<ITrxTypeRepository> _trxTypeRepo = new();
    private readonly Mock<IInvItemRepository> _itemRepo = new();
    private readonly Mock<IInvStockBalanceRepository> _stockBalanceRepo = new();
    private readonly Mock<IInvPriceListRepository> _priceListRepo = new();
    private readonly Mock<ITaxRepository> _taxRepo = new();
    private readonly Mock<IPosShiftRepository> _shiftRepo = new();
    private readonly Mock<ILogger<InvoiceBusinessValidationService>> _logger = new();

    private readonly InvoiceBusinessValidationService _service;

    public InvoiceBusinessValidationServiceTests()
    {
        _service = new InvoiceBusinessValidationService(
            _fiscalRepo.Object,
            _customerRepo.Object,
            _arRepo.Object,
            _trxTypeRepo.Object,
            _itemRepo.Object,
            _stockBalanceRepo.Object,
            _priceListRepo.Object,
            _taxRepo.Object,
            _shiftRepo.Object,
            _logger.Object);
    }

    [Fact]
    public async Task ValidateWarehouseDocument_ShouldFail_WhenFiscalPeriodClosed()
    {
        // Arrange: Document date falls in CLOSED period
        var docDate = new DateTime(2025, 1, 15);
        _fiscalRepo.Setup(f => f.GetPeriodByDateOnlyAsync(docDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlFiscalPeriod { Status = "HARD_CLOSE", StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 1, 31) });

        var dto = new CreateTrxDocumentDto
        {
            BranchId = 1,
            DocYear = 2025,
            DocType = 100,
            TrxType = 1001,
            DocDate = docDate,
            Lines = new List<CreateTrxDocumentLineDto>
            {
                new() { ItemId = 10, QuantityOut = 5, UnitPrice = 100 }
            }
        };

        // Act
        var result = await _service.ValidateWarehouseDocumentDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.FiscalPeriodClosed);
    }

    [Fact]
    public async Task ValidateWarehouseDocument_ShouldFail_WhenStockInsufficientAndNegativeStockNotAllowed()
    {
        // Arrange
        var docDate = DateTime.UtcNow;
        _fiscalRepo.Setup(f => f.GetPeriodByDateOnlyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlFiscalPeriod { Status = "OPEN" });

        _trxTypeRepo.Setup(t => t.GetDocTypeAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxDocType { TypeCode = 100, IsActive = true });

        _trxTypeRepo.Setup(t => t.GetTrxTypeAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxTransactionType { TrxCode = 1001, IsActive = true, AffectsStock = true, StockDirection = -1 });

        // Item does NOT allow negative stock
        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemCode = "ITM-01", ItemNameLocal = "Test Item", IsActive = true, AllowNegativeStock = false });

        // Stock available is only 2, but requested is 5
        _stockBalanceRepo.Setup(s => s.GetAsync(10, 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvStockBalance { ItemId = 10, WarehouseId = 1, OnHandQty = 2, ReservedQty = 0 });

        var dto = new CreateTrxDocumentDto
        {
            BranchId = 1,
            DocYear = docDate.Year,
            DocType = 100,
            TrxType = 1001,
            DocDate = docDate,
            FromWarehouseId = 1,
            Lines = new List<CreateTrxDocumentLineDto>
            {
                new() { ItemId = 10, QuantityOut = 5, UnitPrice = 100 }
            }
        };

        // Act
        var result = await _service.ValidateWarehouseDocumentDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.InsufficientStock);
    }

    [Fact]
    public async Task ValidateWarehouseDocument_ShouldFail_WhenCustomerCreditLimitExceeded()
    {
        // Arrange
        var docDate = DateTime.UtcNow;
        _fiscalRepo.Setup(f => f.GetPeriodByDateOnlyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlFiscalPeriod { Status = "OPEN" });

        _trxTypeRepo.Setup(t => t.GetDocTypeAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxDocType { TypeCode = 100, IsActive = true });

        _trxTypeRepo.Setup(t => t.GetTrxTypeAsync(1002, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxTransactionType { TrxCode = 1002, IsActive = true, AffectsPartyBalance = true, RequiresParty = true });

        _customerRepo.Setup(c => c.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { Id = 50, CustomerCode = "CUST-001", NameLocal = "Big Corp", IsActive = true, CreditLimit = 1000m });

        // Customer already owes 900, new bill is 500 => Total 1400 > CreditLimit 1000
        _arRepo.Setup(a => a.GetOpenInvoicesAsync("CUST-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArSubledgerTransaction>
            {
                new() { LocalOpenAmount = 900m }
            });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Test Item", IsActive = true, AllowNegativeStock = true });

        var dto = new CreateTrxDocumentDto
        {
            BranchId = 1,
            DocYear = docDate.Year,
            DocType = 100,
            TrxType = 1002,
            DocDate = docDate,
            PartyId = 50,
            Lines = new List<CreateTrxDocumentLineDto>
            {
                new() { ItemId = 10, QuantityOut = 5, UnitPrice = 100 } // 500
            }
        };

        // Act
        var result = await _service.ValidateWarehouseDocumentDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.CreditLimitExceeded);
    }

    [Fact]
    public async Task GetEffectiveTaxRate_ShouldResolveDynamically_WithoutHardcoded15()
    {
        // Arrange: Custom item with 5% tax rate
        var customRate = new TaxRate { Id = 88, TaxRateCode = "VAT_5", RatePercent = 5m, IsActive = true };
        _itemRepo.Setup(i => i.GetByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 20, TaxRateId = 88, IsTaxExempt = false });
        _taxRepo.Setup(t => t.GetTaxRateByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customRate);

        // Act
        var rate = await _service.GetEffectiveTaxRateAsync(20, 1);
        var percent = await _service.GetEffectiveTaxRatePercentAsync(20, 1);

        // Assert
        Assert.NotNull(rate);
        Assert.Equal("VAT_5", rate.TaxRateCode);
        Assert.Equal(5m, percent);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenShiftNotOpen()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 999, Status = PosShiftStatus.AuditedAndClosed });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 999,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 1, Quantity = 1, UnitPrice = 10 }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ShiftId");
    }

    [Fact]
    public async Task ValidateWarehouseDocument_ShouldFail_WhenSalesDocumentHasQuantityIn()
    {
        // Arrange: Sales invoice (StockDirection = -1) with QuantityIn > 0
        var docDate = DateTime.UtcNow;
        _fiscalRepo.Setup(f => f.GetPeriodByDateOnlyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlFiscalPeriod { Status = "OPEN" });
        _trxTypeRepo.Setup(t => t.GetDocTypeAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxDocType { TypeCode = 100, IsActive = true });
        _trxTypeRepo.Setup(t => t.GetTrxTypeAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxTransactionType { TrxCode = 1001, TrxNameLocal = "فاتورة مبيعات", IsActive = true, AffectsStock = true, StockDirection = -1 });
        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Test Item", IsActive = true, AllowNegativeStock = true });

        var dto = new CreateTrxDocumentDto
        {
            BranchId = 1,
            DocYear = docDate.Year,
            DocType = 100,
            TrxType = 1001,
            DocDate = docDate,
            Lines = new List<CreateTrxDocumentLineDto>
            {
                new() { ItemId = 10, QuantityIn = 1, QuantityOut = 0, UnitPrice = 100 }
            }
        };

        // Act
        var result = await _service.ValidateWarehouseDocumentDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Lines[1].QuantityIn");
    }

    [Fact]
    public async Task ValidateWarehouseDocument_ShouldAutoResolveQuantity_WhenUnifiedQuantityProvided()
    {
        // Arrange: Sales invoice with unified Quantity = 5 (QuantityIn/Out not specified)
        var docDate = DateTime.UtcNow;
        _fiscalRepo.Setup(f => f.GetPeriodByDateOnlyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlFiscalPeriod { Status = "OPEN" });
        _trxTypeRepo.Setup(t => t.GetDocTypeAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxDocType { TypeCode = 100, IsActive = true });
        _trxTypeRepo.Setup(t => t.GetTrxTypeAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrxTransactionType { TrxCode = 1001, TrxNameLocal = "فاتورة مبيعات", IsActive = true, AffectsStock = true, StockDirection = -1 });
        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Test Item", IsActive = true, AllowNegativeStock = true });

        var line = new CreateTrxDocumentLineDto { ItemId = 10, Quantity = 5, UnitPrice = 100 };
        var dto = new CreateTrxDocumentDto
        {
            BranchId = 1,
            DocYear = docDate.Year,
            DocType = 100,
            TrxType = 1001,
            DocDate = docDate,
            Lines = new List<CreateTrxDocumentLineDto> { line }
        };

        // Act
        var result = await _service.ValidateWarehouseDocumentDtoAsync(dto);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(5, line.QuantityOut);
        Assert.Equal(0, line.QuantityIn);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenTillDoesNotMatchShift()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 10, Status = PosShiftStatus.Open });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 99, // Mismatched TillId
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 1, Quantity = 1, UnitPrice = 10 }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "TillId");
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenLinesEmpty()
    {
        // Arrange
        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>()
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Lines");
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenItemNotActiveOrNotFound()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvItem?)null);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 999, ItemName = "Ghost Item", Quantity = 1, UnitPrice = 10 }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.ItemNotFound);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenItemNotShowInPos()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Raw Material", IsActive = true, ShowInPos = false });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 1, UnitPrice = 10 }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.UnauthorizedAction);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenQuantityIsZeroOrNegative()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Soda", IsActive = true, ShowInPos = true, AllowNegativeStock = true });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 0, UnitPrice = 10 }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.PositiveNumberRequired);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenInsufficientStockAndNegativeStockForbidden()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Limited Juice", IsActive = true, ShowInPos = true, AllowNegativeStock = false });

        _stockBalanceRepo.Setup(s => s.GetByItemAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvStockBalance>
            {
                new() { ItemId = 10, WarehouseId = 1, OnHandQty = 2, ReservedQty = 0 }
            });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 5, UnitPrice = 10 } // Request 5, only 2 available
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.InsufficientStock);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenPriceBelowMinPrice()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "VIP Box", IsActive = true, ShowInPos = true, AllowNegativeStock = true });

        _priceListRepo.Setup(p => p.GetPriceListItemByIdAsync(5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvPriceListItem { PriceListId = 5, ItemId = 10, MinPrice = 50m });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            PriceListId = 5,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 1, UnitPrice = 30m } // 30 < MinPrice 50
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenDiscountPercentOutOfRange()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Coffee", IsActive = true, ShowInPos = true, AllowNegativeStock = true });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            ManualDiscountPercent = 150m, // Invalid (> 100)
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 1, UnitPrice = 15m }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ManualDiscountPercent");
    }

    [Fact]
    public async Task ValidatePosOrder_ShouldFail_WhenPaymentAmountNonPositive()
    {
        // Arrange
        _shiftRepo.Setup(s => s.GetShiftByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosShift { Id = 1, TillId = 1, Status = PosShiftStatus.Open });

        _itemRepo.Setup(i => i.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvItem { Id = 10, ItemNameLocal = "Tea", IsActive = true, ShowInPos = true, AllowNegativeStock = true });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 1,
            TillId = 1,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { ItemId = 10, Quantity = 1, UnitPrice = 10m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = -10m }
            }
        };

        // Act
        var result = await _service.ValidatePosOrderDtoAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Payments");
    }
}
