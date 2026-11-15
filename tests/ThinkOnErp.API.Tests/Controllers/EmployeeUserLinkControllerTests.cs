using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyEmployeeLink;
using ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyTeam;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

public sealed class EmployeeUserLinkControllerTests
{
    private static EmployeesController Create(Mock<IMediator> mediator, string? userId)
    {
        var controller = new EmployeesController(mediator.Object);
        var claims = userId == null ? Array.Empty<Claim>() : new[] { new Claim("userId", userId) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return controller;
    }

    [Fact]
    public void AccountLinkChangesRequireAdministratorPolicy()
    {
        var method = typeof(EmployeesController).GetMethod(nameof(EmployeesController.LinkEmployeeUser))!;
        Assert.Equal("AdminOnly", method.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    public async Task MyLinkRejectsMissingOrInvalidIdentity(string? userId)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var result = await Create(mediator, userId).GetMyEmployeeLink(default);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task MyLinkUsesAuthenticatedUserId()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<GetMyEmployeeLinkQuery>(q => q.UserId == 12), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EmployeeUserLinkDto>.CreateSuccess(new EmployeeUserLinkDto("EMP1", 12)));
        var result = await Create(mediator, "12").GetMyEmployeeLink(default);
        Assert.IsType<OkObjectResult>(result.Result);
        mediator.Verify(m => m.Send(It.Is<GetMyEmployeeLinkQuery>(q => q.UserId == 12), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MyLinkReturnsNotFoundForUnlinkedAccount()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetMyEmployeeLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EmployeeUserLinkDto>.CreateFailure("No employee is linked to this account.", statusCode: 404));
        var result = await Create(mediator, "12").GetMyEmployeeLink(default);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    public async Task MyTeamRejectsMissingOrInvalidIdentity(string? userId)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var result = await Create(mediator, userId).GetMyTeam();
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task MyTeamUsesAuthenticatedIdentityAndExplicitPaging()
    {
        var expected = new GetMyTeamQuery(12, false, "name", 2, 5);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>.CreateSuccess(
                new PagedResultDto<EmployeeTeamMemberDto>(Array.Empty<EmployeeTeamMemberDto>(), 0, 2, 5)));
        var result = await Create(mediator, "12").GetMyTeam(false, "name", 2, 5);
        Assert.IsType<OkObjectResult>(result.Result);
        mediator.Verify(m => m.Send(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TeamRelationshipDoesNotEnableSelfServiceFallback()
    {
        var permission = typeof(EmployeesController).GetMethod(nameof(EmployeesController.GetMyTeam))!.GetCustomAttribute<HrPermissionAttribute>()!;
        Assert.Equal("hr-employees", permission.Screen);
        Assert.Equal("view", permission.Feature);
        Assert.False(permission.SelfService);
    }
}
