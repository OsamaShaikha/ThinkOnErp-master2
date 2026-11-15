using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class EndOfServiceControllerTests
{
    private static EndOfServiceSettlementDto MakeDto(long id = 77, string status = "DRAFT") => new(
        id, 2L, null, "EMP-2", "John Doe", "TERMINATION", null, null,
        new DateTime(2026, 11, 1), new DateTime(2026, 11, 1),
        2.5m, 30, 15, 5000m, 5000m, "JOD",
        5000m, 0m, 500m, 10m, 0m, 0m, 5500m,
        0m, 0m, 0m, 0m, 0m, 5500m,
        status, null, null, null, null, null, null,
        new List<EndOfServiceLineDto>());

    [Fact]
    public async Task CalculateSettlement_PassesResolvedTenantCompanyToService()
    {
        var request = new CreateEndOfServiceRequest(
            "EMP-2", "TERMINATION", new DateTime(2026, 11, 1));
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.CalculateSettlementAsync(
                request, "HR_USER", 2L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto());
        var controller = BuildController(service.Object, companyId: 2, userName: "HR_USER");

        var result = await controller.CalculateSettlement(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(s => s.CalculateSettlementAsync(
            request, "HR_USER", 2L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateSettlement_RefusesWhenTenantContextIsMissing()
    {
        var request = new CreateEndOfServiceRequest(
            "EMP-2", "TERMINATION", new DateTime(2026, 11, 1));
        var service = new Mock<IEndOfServiceService>();
        var controller = new EndOfServiceController(
            service.Object,
            Mock.Of<IHrAccountingHandoffService>(),
            NullLogger<EndOfServiceController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.CalculateSettlement(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        service.Verify(s => s.CalculateSettlementAsync(
            It.IsAny<CreateEndOfServiceRequest>(), It.IsAny<string>(), It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculateSettlement_UsesSystemWhenUserIdentityNameIsNull()
    {
        var request = new CreateEndOfServiceRequest("EMP-9", "TERMINATION", new DateTime(2026, 12, 1));
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.CalculateSettlementAsync(request, "SYSTEM", 5L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto());
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // no name claim → "SYSTEM"
        };
        http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(5, "T5", "S5");
        var controller = new EndOfServiceController(
            service.Object, Mock.Of<IHrAccountingHandoffService>(),
            NullLogger<EndOfServiceController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = http } };

        await controller.CalculateSettlement(request, CancellationToken.None);

        service.Verify(s => s.CalculateSettlementAsync(request, "SYSTEM", 5L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveSettlement_ForwardsIdAndActorToService()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.ApproveSettlementAsync(77, "APPROVER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto(status: "APPROVED"));
        var controller = BuildController(service.Object, companyId: 2, userName: "APPROVER");

        var result = await controller.ApproveSettlement(77, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(s => s.ApproveSettlementAsync(77, "APPROVER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelSettlement_ForwardsReasonAndUser()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.CancelSettlementAsync(77, "Data entry error", "MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto(status: "CANCELLED"));
        var controller = BuildController(service.Object, companyId: 2, userName: "MANAGER");
        var cancelRequest = new CancelEndOfServiceRequest("Data entry error");

        var result = await controller.CancelSettlement(77, cancelRequest, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(s => s.CancelSettlementAsync(77, "Data entry error", "MANAGER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelSettlement_ReturnsBadRequestWhenReasonIsWhitespace()
    {
        var service = new Mock<IEndOfServiceService>(MockBehavior.Strict);
        var controller = BuildController(service.Object, companyId: 2, userName: "MANAGER");
        var cancelRequest = new CancelEndOfServiceRequest("   ");

        var result = await controller.CancelSettlement(77, cancelRequest, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetById_ReturnsOkWhenFound()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(MakeDto(55));
        var controller = BuildController(service.Object);

        var result = await controller.GetById(55, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenMissing()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlementDto)null!);
        var controller = BuildController(service.Object);

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByEmployeeCode_ReturnsOkWhenFound()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetByEmployeeCodeAsync("EMP-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto());
        var controller = BuildController(service.Object);

        var result = await controller.GetByEmployeeCode("EMP-10", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByEmployeeCode_ReturnsNotFoundWhenNoSettlement()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetByEmployeeCodeAsync("GHOST", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlementDto)null!);
        var controller = BuildController(service.Object);

        var result = await controller.GetByEmployeeCode("GHOST", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetList_ReturnsOkWithResultsList()
    {
        var dtos = new List<EndOfServiceSettlementDto> { MakeDto(1), MakeDto(2) };
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetListAsync(2L, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
        var controller = BuildController(service.Object);

        var result = await controller.GetList(2L, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>(ok.Value);
        Assert.Equal(2, body.Data!.Count);
    }

    [Fact]
    public async Task GetHandoffStatus_ReturnsNotFoundWhenMissing()
    {
        var service = new Mock<IEndOfServiceService>();
        service.Setup(s => s.GetHandoffStatusAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceHandoffStatusDto)null!);
        var controller = BuildController(service.Object);

        var result = await controller.GetHandoffStatus(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static EndOfServiceController BuildController(
        IEndOfServiceService service,
        long companyId = 2,
        string userName = "SYSTEM")
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, userName) }, "test"))
        };
        http.Items[TenantRequestContext.HttpContextItemKey] =
            new TenantRequestContext(companyId, $"T{companyId}", $"S{companyId}");
        return new EndOfServiceController(
            service,
            Mock.Of<IHrAccountingHandoffService>(),
            NullLogger<EndOfServiceController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = http } };
    }
}
