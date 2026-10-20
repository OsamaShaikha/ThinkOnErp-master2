using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using Xunit;

namespace ThinkOnErp.HR.Tests.Validation;

public class PosCustomerAccountPaymentTests
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

    private readonly Mock<ICustomerRepository> _customerRepo = new();
    private readonly Mock<IPaymentMethodRepository> _paymentMethodRepo = new();
    private readonly Mock<IGlVoucherService> _voucherService = new();
    private readonly Mock<IGlFiscalPeriodRepository> _fiscalPeriodRepo = new();
    private readonly Mock<IArSubledgerRepository> _arSubledgerRepo = new();

    private readonly PosOrderService _posOrderService;

    public PosCustomerAccountPaymentTests()
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
            _orderLogger.Object,
            _customerRepo.Object,
            _paymentMethodRepo.Object,
            _voucherService.Object,
            _fiscalPeriodRepo.Object,
            _arSubledgerRepo.Object);

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
                    TaxAmount = 0m,
                    TotalDiscountAmount = 0m,
                    Lines = dto.Lines.Select(l => new CalculatedLineResult
                    {
                        LineNumber = l.LineNumber,
                        ItemId = l.ItemId,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineNetBeforeTax = l.Quantity * l.UnitPrice,
                        TaxAmount = 0m,
                        TaxPercent = 0m,
                        LineTotal = l.Quantity * l.UnitPrice
                    }).ToList()
                };
            });

        _orderRepo
            .Setup(x => x.GenerateNextShiftOrderNumberAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0085");

        _orderRepo
            .Setup(x => x.GenerateNextInvoiceNumberAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("INV-2026-000051");

        _orderRepo
            .Setup(x => x.AddOrderAsync(It.IsAny<PosOrderHeader>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _orderRepo
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _orderRepo
            .Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) => new PosOrderHeader
            {
                Id = id == 0 ? 350 : id,
                BranchId = 1,
                ShiftId = 61,
                OrderNumber = "0085",
                InvoiceNumber = "INV-2026-000051",
                TotalAmount = 8.625m,
                PaidAmount = 8.625m,
                IsPaid = true,
                Status = PosOrderStatus.Completed,
                CustomerId = 21,
                CustomerName = "osama"
            });

        _customerRepo
            .Setup(x => x.GetByIdAsync(21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer
            {
                Id = 21,
                CustomerCode = "CUST_004",
                NameLocal = "اسامة",
                ArControlAccountCode = "112101",
                PaymentTermsDays = 30
            });

        _paymentMethodRepo
            .Setup(x => x.GetByIdAsync(43, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethod
            {
                Id = 43,
                BranchId = 1,
                Code = "CUSTOMER_ACCOUNT",
                NameLocal = "حساب عميل (آجل)",
                MethodType = "ACCOUNT",
                GlAccountCode = "112101"
            });

        _paymentMethodRepo
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethod
            {
                Id = 99,
                BranchId = 1,
                Code = "CUSTOMER_ACCOUNT",
                NameLocal = "حساب عميل (آجل)",
                MethodType = "ACCOUNT",
                GlAccountCode = "112101"
            });

        _voucherService
            .Setup(x => x.CreateVoucherAsync(It.IsAny<CreateGlVoucherDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlVoucherHeaderDto
            {
                Id = 122,
                VoucherNo = 101001,
                VoucherType = 101
            });

        _voucherService
            .Setup(x => x.PostVoucherAsync(122, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlVoucherHeaderDto
            {
                Id = 122,
                VoucherNo = 101001,
                Status = 3
            });
    }

    [Fact]
    public async Task CreateOrder_WithDatabasePaymentMethodId43_ResolvesAsCustomerAccount_IncrementsShift_AndRecordsReceivable()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashSales = 0m,
            TotalCustomerAccountSales = 0m
        };

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 21,
            CustomerName = "osama",
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Office Chair", Quantity = 1.0m, UnitPrice = 8.625m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 43, Amount = 8.625m, TenderedAmount = 8.625m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(8.625m, shift.TotalCustomerAccountSales);
        Assert.Equal(0m, shift.TotalCashSales);

        // Verify GL Voucher was created and posted for customer CUST_004
        _voucherService.Verify(x => x.CreateVoucherAsync(
            It.Is<CreateGlVoucherDto>(v =>
                v.VoucherType == 101 &&
                v.Details.Any(d => d.AccountCode == "112101" && d.Debit == 8.625m && d.PartyType == "CUSTOMER" && d.PartyCode == "CUST_004") &&
                v.Details.Any(d => d.AccountCode == "411101" && d.Credit == 8.625m)),
            "admin",
            It.IsAny<CancellationToken>()), Times.Once);

        _voucherService.Verify(x => x.PostVoucherAsync(122, "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithCustomerAccountEnum_IncrementsShiftCustomerAccountSales_AndRecordsReceivable()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashSales = 0m,
            TotalCustomerAccountSales = 0m
        };

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 21,
            CustomerName = "osama",
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Office Chair", Quantity = 1.0m, UnitPrice = 5.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = (int)PosPaymentMethod.CustomerAccount, Amount = 5.0m, TenderedAmount = 5.0m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(5.0m, shift.TotalCustomerAccountSales);

        _voucherService.Verify(x => x.CreateVoucherAsync(
            It.Is<CreateGlVoucherDto>(v =>
                v.Details.Any(d => d.PartyCode == "CUST_004" && d.Debit == 5.0m)),
            "admin",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithExplicitPaymentMethod99_ResolvesAsCustomerAccount_IncrementsShift_AndRecordsReceivable()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashSales = 0m,
            TotalCustomerAccountSales = 0m
        };

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 21,
            CustomerName = "osama",
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Office Chair", Quantity = 2.0m, UnitPrice = 10.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 99, Amount = 20.0m, TenderedAmount = 20.0m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(20.0m, shift.TotalCustomerAccountSales);

        _orderRepo.Verify(x => x.AddOrderAsync(
            It.Is<PosOrderHeader>(o =>
                o.Payments.Any(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount && (int)p.PaymentMethod == 99 && p.Amount == 20.0m)),
            It.IsAny<CancellationToken>()), Times.Once);

        _voucherService.Verify(x => x.CreateVoucherAsync(
            It.Is<CreateGlVoucherDto>(v =>
                v.VoucherType == 101 &&
                v.Details.Any(d => d.AccountCode == "112101" && d.Debit == 20.0m && d.PartyType == "CUSTOMER" && d.PartyCode == "CUST_004") &&
                v.Details.Any(d => d.AccountCode == "411101" && d.Credit == 20.0m)),
            "admin",
            It.IsAny<CancellationToken>()), Times.Once);

        _voucherService.Verify(x => x.PostVoucherAsync(122, "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithCustomerAccount_WithoutCustomerId_ReturnsBadRequest()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open
        };

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = null, // No customer selected!
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, Quantity = 1.0m, UnitPrice = 8.625m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 43, Amount = 8.625m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("يجب تحديد واختيار العميل عند الدفع على الحساب", result.Message);
    }

    [Fact]
    public async Task RefundOrder_WhenOriginalWasPaidByCustomerAccount_IssuesARCredit_AndDoesNotDeductCashInDrawer()
    {
        // Arrange
        var originalOrder = new PosOrderHeader
        {
            Id = 350,
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            OrderNumber = "0085",
            InvoiceNumber = "INV-2026-000051",
            TotalAmount = 8.625m,
            PaidAmount = 8.625m,
            IsPaid = true,
            CustomerId = 21,
            CustomerName = "osama",
            Lines = new List<PosOrderLine>
            {
                new() { Id = 501, LineNumber = 1, ItemId = 81, Quantity = 1.0m, UnitPrice = 8.625m, LineTotal = 8.625m }
            },
            Payments = new List<PosOrderPayment>
            {
                new() { Id = 289, PaymentMethod = (PosPaymentMethod)43, Amount = 8.625m }
            }
        };

        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open,
            TotalCashRefunds = 0m,
            TotalCardRefunds = 0m
        };

        _orderRepo.Setup(x => x.GetOrderByIdAsync(350, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalOrder);

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var refundDto = new RefundOrderDto
        {
            OrderId = 350,
            RefundReason = "Customer return",
            RefundLines = new List<RefundOrderLineDto>
            {
                new() { OrderLineId = 501, QuantityToRefund = 1.0m }
            }
        };

        // Act
        var result = await _posOrderService.RefundOrderAsync(refundDto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        // Cash drawer should NOT be impacted
        Assert.Equal(0m, shift.TotalCashRefunds);
        Assert.Equal(0m, shift.TotalCardRefunds);

        // AR Credit voucher should be created to reverse customer debt
        _voucherService.Verify(x => x.CreateVoucherAsync(
            It.Is<CreateGlVoucherDto>(v =>
                v.VoucherType == 101 &&
                v.Details.Any(d => d.AccountCode == "112101" && d.Credit == 8.625m && d.PartyType == "CUSTOMER" && d.PartyCode == "CUST_004")),
            "admin",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithHospitality98_IncrementsHospitalitySales_AndSetsPaymentMethod98()
    {
        // Arrange
        var shift = new PosShift
        {
            Id = 61,
            BranchId = 2, // Testing another branch!
            Status = PosShiftStatus.Open,
            TotalCashSales = 0m,
            TotalHospitalitySales = 0m
        };

        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 2, // Branch 2
            ShiftId = 61,
            TillId = 41,
            CustomerId = null,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Coffee", Quantity = 1.0m, UnitPrice = 15.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 98, Amount = 15.0m, TenderedAmount = 15.0m }
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(15.0m, shift.TotalHospitalitySales);

        _orderRepo.Verify(x => x.AddOrderAsync(
            It.Is<PosOrderHeader>(o =>
                o.Payments.Any(p => p.PaymentMethod == PosPaymentMethod.Hospitality && (int)p.PaymentMethod == 98 && p.Amount == 15.0m)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerExceedsCreditLimit_ReturnsBadRequest()
    {
        // Arrange
        var customerWithLimit = new Customer
        {
            Id = 30,
            CustomerCode = "CUST_LIMIT",
            NameLocal = "عميل محدود الائتمان",
            CreditLimit = 50m,
            IsActive = true
        };

        _customerRepo.Setup(x => x.GetByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerWithLimit);

        // Current balance is already 45m (debt)
        _arSubledgerRepo.Setup(x => x.GetCustomerBalanceAsync("CUST_LIMIT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(45m);

        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open
        };
        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>())).ReturnsAsync(shift);

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 30,
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, Quantity = 1.0m, UnitPrice = 10.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 99, Amount = 10.0m } // 45 + 10 = 55 > 50 CreditLimit
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("السقف الائتماني", result.Message);
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerHasAdvanceCreditBalance_DeductsFromAdvance()
    {
        // Arrange
        var customerWithAdvance = new Customer
        {
            Id = 31,
            CustomerCode = "CUST_ADV",
            NameLocal = "عميل لديه رصيد مسبق",
            CreditLimit = 100m,
            IsActive = true
        };

        _customerRepo.Setup(x => x.GetByIdAsync(31, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerWithAdvance);

        // Current balance is -30m (credit / advance deposit)
        _arSubledgerRepo.Setup(x => x.GetCustomerBalanceAsync("CUST_ADV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(-30m);

        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open
        };
        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>())).ReturnsAsync(shift);

        _orderRepo.Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosOrderHeader
            {
                Id = 350,
                BranchId = 1,
                ShiftId = 61,
                TotalAmount = 20m,
                PaidAmount = 20m,
                IsPaid = true,
                CustomerId = 31,
                CustomerName = "عميل لديه رصيد مسبق",
                Payments = new List<PosOrderPayment>
                {
                    new()
                    {
                        PaymentMethod = PosPaymentMethod.CustomerAccount,
                        Amount = 20m,
                        TransactionReference = "ADV:20.0000|REC:0.0000"
                    }
                }
            });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 31,
            CustomerName = "عميل لديه رصيد مسبق",
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Item 1", Quantity = 1.0m, UnitPrice = 20.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 99, Amount = 20.0m, TenderedAmount = 20.0m } // Fully covered by 30m advance
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsCreditSale);
        Assert.Equal(20m, result.Data.CustomerAccountAmount);
        Assert.Equal(20m, result.Data.CustomerAdvanceDeducted);
        Assert.Equal(0m, result.Data.CustomerReceivableCharged);
        Assert.True(result.Data.IsPaid);
        Assert.Equal(20m, result.Data.PaidAmount);
        Assert.Equal(0m, result.Data.RemainingAmount);
        Assert.Equal("PaidFromAdvance", result.Data.PaymentStatus);
        Assert.Equal("مدفوع بالكامل من رصيد مقدم (أمانات)", result.Data.PaymentStatusNameAr);
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerPartiallyExceedsAdvance_SplitsBetweenAdvanceAndReceivable()
    {
        // Arrange
        var customerPartial = new Customer
        {
            Id = 32,
            CustomerCode = "CUST_PARTIAL",
            NameLocal = "عميل رصيد جزئي",
            CreditLimit = 100m,
            IsActive = true
        };

        _customerRepo.Setup(x => x.GetByIdAsync(32, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerPartial);

        // Current balance is -10m (10m advance)
        _arSubledgerRepo.Setup(x => x.GetCustomerBalanceAsync("CUST_PARTIAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(-10m);

        var shift = new PosShift
        {
            Id = 61,
            BranchId = 1,
            Status = PosShiftStatus.Open
        };
        _shiftRepo.Setup(x => x.GetShiftByIdAsync(61, It.IsAny<CancellationToken>())).ReturnsAsync(shift);

        _orderRepo.Setup(x => x.GetOrderByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosOrderHeader
            {
                Id = 350,
                BranchId = 1,
                ShiftId = 61,
                TotalAmount = 25m,
                PaidAmount = 10m, // Only 10m covered from advance
                IsPaid = false,   // Not fully paid because 15m is credit
                CustomerId = 32,
                CustomerName = "عميل رصيد جزئي",
                Payments = new List<PosOrderPayment>
                {
                    new()
                    {
                        PaymentMethod = PosPaymentMethod.CustomerAccount,
                        Amount = 25m,
                        TransactionReference = "ADV:10.0000|REC:15.0000"
                    }
                }
            });

        var dto = new CreatePosOrderDto
        {
            BranchId = 1,
            ShiftId = 61,
            TillId = 41,
            CustomerId = 32,
            CustomerName = "عميل رصيد جزئي",
            Lines = new List<CreatePosOrderLineDto>
            {
                new() { LineNumber = 1, ItemId = 81, ItemCode = "ITM-90001", ItemName = "Item 1", Quantity = 1.0m, UnitPrice = 25.0m }
            },
            Payments = new List<CreatePosOrderPaymentDto>
            {
                new() { PaymentMethod = 99, Amount = 25.0m, TenderedAmount = 25.0m } // 10m from advance, 15m to new receivable
            }
        };

        // Act
        var result = await _posOrderService.CreateOrderAsync(dto, "admin");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsCreditSale);
        Assert.Equal(25m, result.Data.CustomerAccountAmount);
        Assert.Equal(10m, result.Data.CustomerAdvanceDeducted);
        Assert.Equal(15m, result.Data.CustomerReceivableCharged);
        Assert.False(result.Data.IsPaid);
        Assert.Equal(10m, result.Data.PaidAmount);
        Assert.Equal(15m, result.Data.RemainingAmount);
        Assert.Equal("PartiallyPaidFromAdvance", result.Data.PaymentStatus);
        Assert.Equal("مدفوع جزئياً من رصيد مقدم (والباقي ذمم)", result.Data.PaymentStatusNameAr);
    }

    [Fact]
    public void PosOrderHeader_PaymentStatus_ReturnsExpectedStatusForEachScenario()
    {
        // 1. Paid in Cash
        var cashOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 50m,
            IsPaid = true,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 50m }
            }
        };
        Assert.Equal(PosPaymentStatus.Paid, cashOrder.PaymentStatus);
        Assert.Equal(0m, cashOrder.RemainingAmount);

        // 2. PaidFromAdvance
        var advOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 50m,
            IsPaid = true,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.CustomerAccount, Amount = 50m, TransactionReference = "ADV:50.0000|REC:0.0000" }
            }
        };
        Assert.Equal(PosPaymentStatus.PaidFromAdvance, advOrder.PaymentStatus);
        Assert.Equal(0m, advOrder.RemainingAmount);

        // 3. CustomerAccount (Pure credit sale)
        var creditOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 0m,
            IsPaid = false,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.CustomerAccount, Amount = 50m, TransactionReference = "ADV:0.0000|REC:50.0000" }
            }
        };
        Assert.Equal(PosPaymentStatus.CustomerAccount, creditOrder.PaymentStatus);
        Assert.Equal(50m, creditOrder.RemainingAmount);

        // 4. PartiallyPaidFromAdvance (Option B)
        var partialAdvOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 20m,
            IsPaid = false,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.CustomerAccount, Amount = 50m, TransactionReference = "ADV:20.0000|REC:30.0000" }
            }
        };
        Assert.Equal(PosPaymentStatus.PartiallyPaidFromAdvance, partialAdvOrder.PaymentStatus);
        Assert.Equal(30m, partialAdvOrder.RemainingAmount);

        // 5. Mixed: Cash + Credit (PartiallyPaid)
        var mixedOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 20m,
            IsPaid = false,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.Cash, Amount = 20m },
                new() { PaymentMethod = PosPaymentMethod.CustomerAccount, Amount = 30m, TransactionReference = "ADV:0.0000|REC:30.0000" }
            }
        };
        Assert.Equal(PosPaymentStatus.PartiallyPaid, mixedOrder.PaymentStatus);
        Assert.Equal(30m, mixedOrder.RemainingAmount);

        // 6. Hospitality
        var hospOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 0m,
            IsPaid = true,
            Payments = new List<PosOrderPayment>
            {
                new() { PaymentMethod = PosPaymentMethod.Hospitality, Amount = 50m }
            }
        };
        Assert.Equal(PosPaymentStatus.Hospitality, hospOrder.PaymentStatus);
        Assert.Equal(0m, hospOrder.RemainingAmount);

        // 7. Unpaid (Open draft order)
        var unpaidOrder = new PosOrderHeader
        {
            TotalAmount = 50m,
            PaidAmount = 0m,
            IsPaid = false,
            Payments = new List<PosOrderPayment>()
        };
        Assert.Equal(PosPaymentStatus.Unpaid, unpaidOrder.PaymentStatus);
        Assert.Equal(50m, unpaidOrder.RemainingAmount);
    }
}
