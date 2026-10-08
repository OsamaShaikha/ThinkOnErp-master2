using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

public sealed class EmployeeUserLinkControllerTests
{
    private static EmployeesController Create(Mock<IEmployeeService> service, string? userId)
    {
        var controller = new EmployeesController(service.Object, Mock.Of<IEmployeeExcelService>(), NullLogger<EmployeesController>.Instance);
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
        var service = new Mock<IEmployeeService>(MockBehavior.Strict);
        var result = await Create(service, userId).GetMyEmployeeLink(default);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task MyLinkUsesAuthenticatedUserId()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetEmployeeUserLinkAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeUserLinkDto("EMP1", 12));
        var result = await Create(service, "12").GetMyEmployeeLink(default);
        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(s => s.GetEmployeeUserLinkAsync(12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MyLinkReturnsNotFoundForUnlinkedAccount()
    {
        var result = await Create(new Mock<IEmployeeService>(), "12").GetMyEmployeeLink(default);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
