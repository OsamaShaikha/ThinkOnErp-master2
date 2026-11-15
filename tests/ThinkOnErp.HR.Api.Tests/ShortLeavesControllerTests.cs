using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class ShortLeavesControllerTests
{
    [Fact]
    public async Task SubmitRequest_UsesResolvedTenantWhenDtoOmitsCompanyId()
    {
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.Is<CreateShortLeaveDto>(dto => dto.CompanyId == 2),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult(2));
        var controller = CreateController(service.Object, new TenantRequestContext(2, "TENANT_2", "HR_TENANT_2"));
        var request = new CreateShortLeaveDto(
            "EMP-2", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        service.Verify(s => s.SubmitRequestAsync(
            It.Is<CreateShortLeaveDto>(dto => dto.CompanyId == 2),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitRequest_RejectsNonDefaultCompanyThatDiffersFromTenant()
    {
        var service = new Mock<IShortLeaveService>();
        var controller = CreateController(service.Object, new TenantRequestContext(2, "TENANT_2", "HR_TENANT_2"));
        var request = new CreateShortLeaveDto(
            "EMP-2", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic", CompanyId: 3);

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        service.Verify(s => s.SubmitRequestAsync(
            It.IsAny<CreateShortLeaveDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitRequest_RejectsExplicitCompanyOneWhenResolvedTenantIsTwo()
    {
        var service = new Mock<IShortLeaveService>();
        var controller = CreateController(service.Object, new TenantRequestContext(2, "TENANT_2", "HR_TENANT_2"));
        var request = new CreateShortLeaveDto(
            "EMP-2", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic", CompanyId: 1);

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        service.Verify(s => s.SubmitRequestAsync(
            It.IsAny<CreateShortLeaveDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitRequest_AllowsExplicitCompanyMatchingTenant()
    {
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.Is<CreateShortLeaveDto>(dto => dto.CompanyId == 3),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult(3));
        var controller = CreateController(service.Object, new TenantRequestContext(3, "TENANT_3", "HR_TENANT_3"));
        var request = new CreateShortLeaveDto(
            "EMP-3", "EARLY_DEPARTURE", new DateTime(2026, 11, 14),
            TimeSpan.FromHours(13), TimeSpan.FromHours(14), "Medical", CompanyId: 3);

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        service.Verify(s => s.SubmitRequestAsync(
            It.Is<CreateShortLeaveDto>(dto => dto.CompanyId == 3),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitRequest_ReturnsForbidWhenTenantContextMissing()
    {
        var service = new Mock<IShortLeaveService>(MockBehavior.Strict);
        var controller = CreateController(service.Object, tenant: null);
        var request = new CreateShortLeaveDto(
            "EMP-5", "LATE_ARRIVAL", new DateTime(2026, 11, 15),
            TimeSpan.FromHours(9), TimeSpan.FromHours(10), "Traffic");

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitRequest_EarlyDepartureTypeIsAccepted()
    {
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.Is<CreateShortLeaveDto>(dto => dto.PermissionType == "EARLY_DEPARTURE"),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult(1) with { PermissionType = "EARLY_DEPARTURE" });
        var controller = CreateController(service.Object, new TenantRequestContext(1, "TENANT_1", "HR_TENANT_1"));
        var request = new CreateShortLeaveDto(
            "EMP-1", "EARLY_DEPARTURE", new DateTime(2026, 11, 20),
            TimeSpan.FromHours(13), TimeSpan.FromHours(14), "Child sick");

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task SubmitRequest_PassesCancellationTokenToService()
    {
        using var cts = new CancellationTokenSource();
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.IsAny<CreateShortLeaveDto>(),
                It.IsAny<string>(), cts.Token))
            .ReturnsAsync(CreateResult(2));
        var controller = CreateController(service.Object, new TenantRequestContext(2, "T2", "S2"));
        var request = new CreateShortLeaveDto(
            "EMP-6", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Late bus");

        await controller.SubmitRequest(request, cts.Token);

        service.Verify(s => s.SubmitRequestAsync(
            It.IsAny<CreateShortLeaveDto>(), It.IsAny<string>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task SubmitRequest_InjectsTenantCompanyIdIntoDto()
    {
        long capturedCompanyId = -1;
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.IsAny<CreateShortLeaveDto>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<CreateShortLeaveDto, string, CancellationToken>((dto, _, _) => capturedCompanyId = dto.CompanyId)
            .ReturnsAsync(CreateResult(5));
        var controller = CreateController(service.Object, new TenantRequestContext(5, "T5", "S5"));
        var request = new CreateShortLeaveDto(
            "EMP-7", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");

        await controller.SubmitRequest(request, CancellationToken.None);

        Assert.Equal(5, capturedCompanyId);
    }

    [Fact]
    public async Task SubmitRequest_ZeroCompanyIdIsOverriddenByTenant()
    {
        var service = new Mock<IShortLeaveService>();
        service.Setup(s => s.SubmitRequestAsync(
                It.Is<CreateShortLeaveDto>(dto => dto.CompanyId == 4),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult(4));
        var controller = CreateController(service.Object, new TenantRequestContext(4, "T4", "S4"));
        // CompanyId = 0 (default long) should be treated as "not specified" → overridden by tenant
        var request = new CreateShortLeaveDto(
            "EMP-8", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");

        var result = await controller.SubmitRequest(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    private static ShortLeavesController CreateController(
        IShortLeaveService service,
        TenantRequestContext? tenant)
    {
        var httpContext = new DefaultHttpContext();
        if (tenant != null)
            httpContext.Items[TenantRequestContext.HttpContextItemKey] = tenant;
        return new ShortLeavesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ShortLeaveDto CreateResult(long companyId) => new(
        1, companyId, null, "EMP-2", "Employee", "LATE_ARRIVAL", new DateTime(2026, 11, 13),
        TimeSpan.FromHours(8), TimeSpan.FromHours(9), 60, "Traffic", "PENDING_APPROVAL", "PAID",
        0m, 0m, null, null, null, null, null, null, null, null, DateTime.UtcNow);
}
