using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollAdjustmentDecisionControllerTests
{
    [Fact]
    public async Task CreateOvertimeAdjustment_ForwardsSourceAndActor()
    {
        var service = new Mock<IPayrollAdjustmentService>(MockBehavior.Strict);
        var dto = new CreateOvertimePayrollAdjustmentDto(3, "E1", new DateTime(2026, 11, 5), "2026-11", "OT_ALLOWANCE", 7);
        service.Setup(s => s.CreateOvertimeAdjustmentAsync(dto, "SYSTEM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollAdjustment { Id = 55, Status = "PENDING", SourceType = "ATTENDANCE_OVERTIME" });
        var controller = new PayrollAdjustmentsController(service.Object, NullLogger<PayrollAdjustmentsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var action = await controller.CreateOvertimeAdjustment(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(action.Result);
        service.VerifyAll();
    }

    [Theory]
    [InlineData("approve", true)]
    [InlineData("post", true)]
    [InlineData("reject", false)]
    public async Task DecisionEndpointsForwardReasonAndActor(string actionName, bool approve)
    {
        var service = new Mock<IPayrollAdjustmentService>(MockBehavior.Strict);
        var adjustment = new PayrollAdjustment { Id = 27, Status = approve ? "APPROVED" : "REJECTED", DecisionReason = "Reviewed" };
        service.Setup(s => s.DecideAdjustmentAsync(27, approve, "Reviewed", "SYSTEM", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adjustment);
        var controller = new PayrollAdjustmentsController(service.Object, NullLogger<PayrollAdjustmentsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var decision = new PayrollAdjustmentDecisionDto("Reviewed", 7);
        var action = actionName switch
        {
            "approve" => await controller.ApproveAdjustment(27, decision, CancellationToken.None),
            "post" => await controller.PostAdjustment(27, decision, CancellationToken.None),
            _ => await controller.RejectAdjustment(27, decision, CancellationToken.None)
        };

        Assert.IsType<OkObjectResult>(action.Result);
        service.VerifyAll();
    }

    [Fact]
    public async Task UnpostOvertimeForwardsPermittedBranch()
    {
        var service = new Mock<IPayrollAdjustmentService>(MockBehavior.Strict);
        service.Setup(s => s.UnpostAdjustmentAsync(27, "SYSTEM", 7, "Reversed for correction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollAdjustment { Id = 27, Status = "PENDING", SourceType = "ATTENDANCE_OVERTIME", SourceBranchId = 7 });
        var controller = new PayrollAdjustmentsController(service.Object, NullLogger<PayrollAdjustmentsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var action = await controller.UnpostAdjustment(27, 7, "Reversed for correction", CancellationToken.None);

        Assert.IsType<OkObjectResult>(action.Result);
        service.VerifyAll();
    }
}
