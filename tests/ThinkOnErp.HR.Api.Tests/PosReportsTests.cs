using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ThinkOnErp.API.Controllers.Pos;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Pos;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public class PosReportsTests
{
    private OracleDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new OracleDbContext(options);
    }

    [Fact]
    public async Task GetSalesSummaryReport_CalculatesCorrectTotals()
    {
        using var context = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = 1L;

        // Seed orders
        context.PosOrderHeaders.AddRange(
            new PosOrderHeader
            {
                Id = 1,
                BranchId = branchId,
                OrderNumber = "ORD-001",
                CreationDate = DateTime.UtcNow,
                SubtotalAmount = 100m,
                DiscountAmount = 10m,
                TaxAmount = 13.5m,
                TotalAmount = 103.5m,
                PaidAmount = 103.5m,
                OrderType = PosOrderType.DineIn,
                Status = PosOrderStatus.Completed,
                Covers = 2,
                IsActive = true
            },
            new PosOrderHeader
            {
                Id = 2,
                BranchId = branchId,
                OrderNumber = "ORD-002",
                CreationDate = DateTime.UtcNow,
                SubtotalAmount = 50m,
                DiscountAmount = 0m,
                TaxAmount = 7.5m,
                TotalAmount = 57.5m,
                PaidAmount = 57.5m,
                OrderType = PosOrderType.Takeaway,
                Status = PosOrderStatus.Completed,
                Covers = 1,
                IsActive = true
            },
            new PosOrderHeader
            {
                Id = 3,
                BranchId = branchId,
                OrderNumber = "REF-001",
                CreationDate = DateTime.UtcNow,
                SubtotalAmount = 20m,
                DiscountAmount = 0m,
                TaxAmount = 3m,
                TotalAmount = 23m,
                PaidAmount = 23m,
                OrderType = PosOrderType.DineIn,
                Status = PosOrderStatus.Completed,
                IsRefund = true,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        var service = new PosReportService(context, NullLogger<PosReportService>.Instance);
        var filter = new PosReportFilterDto
        {
            BranchId = branchId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        var response = await service.GetSalesSummaryReportAsync(filter);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var data = response.Data;
        Assert.Equal(2, data.TotalOrders);
        Assert.Equal(150m, data.GrossSales);
        Assert.Equal(10m, data.TotalDiscount);
        Assert.Equal(140m, data.NetSales);
        Assert.Equal(21m, data.TotalTax);
        Assert.Equal(161m, data.TotalSales);
        Assert.Equal(1, data.RefundOrdersCount);
        Assert.Equal(23m, data.TotalRefundAmount);
        Assert.Equal(138m, data.NetRevenue);
        Assert.Equal(3, data.TotalGuestsCovers);
        Assert.Equal(1, data.DineInOrdersCount);
        Assert.Equal(1, data.TakeawayOrdersCount);
    }

    [Fact]
    public async Task GetSalesByPaymentMethod_GroupsAndCalculatesCorrectly()
    {
        using var context = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = 1L;

        var order1 = new PosOrderHeader
        {
            Id = 10,
            BranchId = branchId,
            CreationDate = DateTime.UtcNow,
            TotalAmount = 100m,
            IsActive = true,
            Status = PosOrderStatus.Completed
        };
        var order2 = new PosOrderHeader
        {
            Id = 11,
            BranchId = branchId,
            CreationDate = DateTime.UtcNow,
            TotalAmount = 200m,
            IsActive = true,
            Status = PosOrderStatus.Completed
        };
        var refundOrder = new PosOrderHeader
        {
            Id = 12,
            BranchId = branchId,
            CreationDate = DateTime.UtcNow,
            TotalAmount = 50m,
            IsRefund = true,
            IsActive = true,
            Status = PosOrderStatus.Completed
        };

        context.PosOrderHeaders.AddRange(order1, order2, refundOrder);

        context.PosOrderPayments.AddRange(
            new PosOrderPayment
            {
                Id = 1,
                OrderId = 10,
                PaymentMethod = PosPaymentMethod.Cash,
                Amount = 100m,
                PaymentDate = DateTime.UtcNow
            },
            new PosOrderPayment
            {
                Id = 2,
                OrderId = 11,
                PaymentMethod = PosPaymentMethod.Card,
                Amount = 200m,
                PaymentDate = DateTime.UtcNow
            },
            new PosOrderPayment
            {
                Id = 3,
                OrderId = 12,
                PaymentMethod = PosPaymentMethod.Cash,
                Amount = 50m,
                PaymentDate = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var service = new PosReportService(context, NullLogger<PosReportService>.Instance);
        var filter = new PosReportFilterDto
        {
            BranchId = branchId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        var response = await service.GetSalesByPaymentMethodReportAsync(filter);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var data = response.Data;
        Assert.Equal(300m, data.TotalCollected);
        Assert.Equal(50m, data.TotalRefunded);
        Assert.Equal(250m, data.NetCollected);

        var cash = data.Methods.FirstOrDefault(m => m.PaymentMethod == PosPaymentMethod.Cash);
        Assert.NotNull(cash);
        Assert.Equal(100m, cash.TotalAmount);
        Assert.Equal(50m, cash.TotalRefunded);
        Assert.Equal(50m, cash.NetAmount);
        Assert.Equal("نقداً", cash.PaymentMethodNameAr);

        var card = data.Methods.FirstOrDefault(m => m.PaymentMethod == PosPaymentMethod.Card);
        Assert.NotNull(card);
        Assert.Equal(200m, card.TotalAmount);
        Assert.Equal(200m, card.NetAmount);
    }

    [Fact]
    public async Task GetCashDrawerVariance_ComputesVarianceProperly()
    {
        using var context = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = 1L;

        var till = new PosTill { Id = 1, BranchId = branchId, TillCode = "TILL-01", TillName = "Main Till" };
        context.PosTills.Add(till);

        context.PosShifts.AddRange(
            new PosShift
            {
                Id = 101,
                BranchId = branchId,
                TillId = 1,
                ShiftNumber = "SH-001",
                OpenedAt = DateTime.UtcNow.AddHours(-8),
                ClosedAt = DateTime.UtcNow,
                OpeningFloat = 500m,
                TotalCashSales = 1200m,
                ExpectedCashInDrawer = 1700m,
                CountedCashAmount = 1680m, // Shortage of 20
                CreationUser = "cashier1"
            },
            new PosShift
            {
                Id = 102,
                BranchId = branchId,
                TillId = 1,
                ShiftNumber = "SH-002",
                OpenedAt = DateTime.UtcNow.AddHours(-4),
                ClosedAt = DateTime.UtcNow,
                OpeningFloat = 500m,
                TotalCashSales = 800m,
                ExpectedCashInDrawer = 1300m,
                CountedCashAmount = 1315m, // Overage of 15
                CreationUser = "cashier2"
            }
        );
        await context.SaveChangesAsync();

        var service = new PosReportService(context, NullLogger<PosReportService>.Instance);
        var filter = new PosReportFilterDto
        {
            BranchId = branchId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        var response = await service.GetCashDrawerVarianceReportAsync(filter);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var data = response.Data;
        Assert.Equal(2, data.TotalShiftsAudited);
        Assert.Equal(20m, data.TotalShortageAmount);
        Assert.Equal(15m, data.TotalOverageAmount);
        Assert.Equal(-5m, data.NetVarianceAmount);
    }

    [Fact]
    public async Task GetHourlySales_DistributesAcross24Hours()
    {
        using var context = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = 1L;

        var now = DateTime.UtcNow;
        var order = new PosOrderHeader
        {
            Id = 50,
            BranchId = branchId,
            CreationDate = new DateTime(now.Year, now.Month, now.Day, 14, 30, 0, DateTimeKind.Utc), // 14:30
            TotalAmount = 250m,
            IsActive = true,
            Status = PosOrderStatus.Completed
        };
        context.PosOrderHeaders.Add(order);
        await context.SaveChangesAsync();

        var service = new PosReportService(context, NullLogger<PosReportService>.Instance);
        var filter = new PosReportFilterDto
        {
            BranchId = branchId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        var response = await service.GetHourlySalesReportAsync(filter);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var data = response.Data;
        Assert.Equal(24, data.Hours.Count);
        Assert.Equal(14, data.PeakHour);
        Assert.Equal(250m, data.PeakHourSales);
        Assert.Equal("14:00 - 15:00", data.PeakHourFormatted);
    }

    [Fact]
    public async Task PosReportsController_InvokesServiceEndpoints()
    {
        using var context = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new PosReportService(context, NullLogger<PosReportService>.Instance);
        var controller = new PosReportsController(service);

        var filter = new PosReportFilterDto { BranchId = 1 };
        var actionResult = await controller.GetSalesSummary(filter, default);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var apiResponse = Assert.IsType<ThinkOnErp.Application.Common.ApiResponse<PosSalesSummaryReportDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
    }
}
