using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.ApproveSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CalculateSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CancelSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServiceHandoffStatus;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementByEmployeeCode;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementById;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlements;
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
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<CalculateSettlementCommand>(c => c.Request == request && c.Username == "HR_USER" && c.CompanyId == 2L),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto()));
        var controller = BuildController(mediator.Object, companyId: 2, userName: "HR_USER");

        var result = await controller.CalculateSettlement(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        mediator.Verify(m => m.Send(
            It.Is<CalculateSettlementCommand>(c => c.Request == request && c.Username == "HR_USER" && c.CompanyId == 2L),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateSettlement_RefusesWhenTenantContextIsMissing()
    {
        var request = new CreateEndOfServiceRequest(
            "EMP-2", "TERMINATION", new DateTime(2026, 11, 1));
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = new EndOfServiceController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.CalculateSettlement(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CalculateSettlement_UsesSystemWhenUserIdentityNameIsNull()
    {
        var request = new CreateEndOfServiceRequest("EMP-9", "TERMINATION", new DateTime(2026, 12, 1));
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<CalculateSettlementCommand>(c => c.Request == request && c.Username == "SYSTEM" && c.CompanyId == 5L),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto()));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // no name claim → "SYSTEM"
        };
        http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(5, "T5", "S5");
        var controller = new EndOfServiceController(mediator.Object)
        { ControllerContext = new ControllerContext { HttpContext = http } };

        await controller.CalculateSettlement(request, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<CalculateSettlementCommand>(c => c.Request == request && c.Username == "SYSTEM" && c.CompanyId == 5L),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveSettlement_ForwardsIdAndActorToService()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<ApproveSettlementCommand>(c => c.SettlementId == 77 && c.Username == "APPROVER"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto(status: "APPROVED")));
        var controller = BuildController(mediator.Object, companyId: 2, userName: "APPROVER");

        var result = await controller.ApproveSettlement(77, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        mediator.Verify(m => m.Send(
            It.Is<ApproveSettlementCommand>(c => c.SettlementId == 77 && c.Username == "APPROVER"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelSettlement_ForwardsReasonAndUser()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<CancelSettlementCommand>(c => c.SettlementId == 77 && c.Reason == "Data entry error" && c.Username == "MANAGER"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto(status: "CANCELLED")));
        var controller = BuildController(mediator.Object, companyId: 2, userName: "MANAGER");
        var cancelRequest = new CancelEndOfServiceRequest("Data entry error");

        var result = await controller.CancelSettlement(77, cancelRequest, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        mediator.Verify(m => m.Send(
            It.Is<CancelSettlementCommand>(c => c.SettlementId == 77 && c.Reason == "Data entry error" && c.Username == "MANAGER"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelSettlement_ReturnsBadRequestWhenReasonIsWhitespace()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = BuildController(mediator.Object, companyId: 2, userName: "MANAGER");
        var cancelRequest = new CancelEndOfServiceRequest("   ");

        var result = await controller.CancelSettlement(77, cancelRequest, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetById_ReturnsOkWhenFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetSettlementByIdQuery>(q => q.SettlementId == 55),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto(55)));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetById(55, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenMissing()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetSettlementByIdQuery>(q => q.SettlementId == 99),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateFailure("End of service settlement not found.", statusCode: 404));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByEmployeeCode_ReturnsOkWhenFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetSettlementByEmployeeCodeQuery>(q => q.EmployeeCode == "EMP-10"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(MakeDto()));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetByEmployeeCode("EMP-10", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByEmployeeCode_ReturnsNotFoundWhenNoSettlement()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetSettlementByEmployeeCodeQuery>(q => q.EmployeeCode == "GHOST"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceSettlementDto>.CreateFailure("No active end of service settlement found for employee 'GHOST'.", statusCode: 404));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetByEmployeeCode("GHOST", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetList_ReturnsOkWithResultsList()
    {
        var dtos = new List<EndOfServiceSettlementDto> { MakeDto(1), MakeDto(2) };
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetSettlementsQuery>(q => q.CompanyId == 2L && q.BranchId == null && q.Status == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>.CreateSuccess(dtos));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetList(2L, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<EndOfServiceSettlementDto>>>(ok.Value);
        Assert.Equal(2, body.Data!.Count);
    }

    [Fact]
    public async Task GetHandoffStatus_ReturnsNotFoundWhenMissing()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(
                It.Is<GetEndOfServiceHandoffStatusQuery>(q => q.SettlementId == 99),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EndOfServiceHandoffStatusDto>.CreateFailure("No accounting handoff found for this settlement.", statusCode: StatusCodes.Status404NotFound));
        var controller = BuildController(mediator.Object);

        var result = await controller.GetHandoffStatus(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static EndOfServiceController BuildController(
        IMediator mediator,
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
        return new EndOfServiceController(mediator)
        { ControllerContext = new ControllerContext { HttpContext = http } };
    }
}
