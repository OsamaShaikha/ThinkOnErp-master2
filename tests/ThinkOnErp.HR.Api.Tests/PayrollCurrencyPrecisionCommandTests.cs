using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollCurrencyPrecisionCommandTests
{
    [Fact]
    public async Task PutNormalizesCodeAndSavesCompanyScopedOverride()
    {
        var repository = new Mock<IPolicyRepository>(MockBehavior.Strict);
        repository.Setup(r => r.SavePayrollCurrencyPrecisionAsync(It.Is<PayrollCurrencyPrecision>(x =>
            x.CompanyId == 31 && x.CurrencyCode == "ACME" && x.DecimalPlaces == 4 &&
            x.CreationUser == "payroll-admin" && x.UpdateUser == null), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = Controller(repository.Object);

        var response = await controller.SetCurrencyPrecision(
            new PayrollCurrencyPrecisionDto { CompanyId = 31, CurrencyCode = " acme ", DecimalPlaces = 4 }, CancellationToken.None);

        var body = Assert.IsType<ApiResponse<PayrollCurrencyPrecisionDto>>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("ACME", body.Data!.CurrencyCode);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task PutAcceptsConfiguredRange(int decimalPlaces)
    {
        var repository = new Mock<IPolicyRepository>();
        var controller = Controller(repository.Object);
        repository.Setup(r => r.SavePayrollCurrencyPrecisionAsync(It.IsAny<PayrollCurrencyPrecision>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await controller.SetCurrencyPrecision(new PayrollCurrencyPrecisionDto
        {
            CompanyId = 31, CurrencyCode = "USD", DecimalPlaces = decimalPlaces
        }, CancellationToken.None);

        repository.Verify(r => r.SavePayrollCurrencyPrecisionAsync(It.Is<PayrollCurrencyPrecision>(x => x.DecimalPlaces == decimalPlaces), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task PutRejectsPrecisionOutsideSupportedRange(int decimalPlaces)
    {
        var repository = new Mock<IPolicyRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => Controller(repository.Object).SetCurrencyPrecision(
            new PayrollCurrencyPrecisionDto { CompanyId = 31, CurrencyCode = "USD", DecimalPlaces = decimalPlaces }, CancellationToken.None));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAndDeleteAreScopedToRequestedCompanyAndNormalizedCode()
    {
        var repository = new Mock<IPolicyRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetPayrollCurrencyPrecisionsAsync(31, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PayrollCurrencyPrecision { CompanyId = 31, CurrencyCode = "USD", DecimalPlaces = 0 } });
        repository.Setup(r => r.DeletePayrollCurrencyPrecisionAsync(31, "USD", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = Controller(repository.Object);

        var get = await controller.GetCurrencyPrecision(31, CancellationToken.None);
        var delete = await controller.DeleteCurrencyPrecision(31, " usd ", CancellationToken.None);

        var getBody = Assert.IsType<ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>>(Assert.IsType<OkObjectResult>(get.Result).Value);
        var deleteBody = Assert.IsType<ApiResponse<bool>>(Assert.IsType<OkObjectResult>(delete.Result).Value);
        Assert.Equal(0, Assert.Single(getBody.Data!).DecimalPlaces);
        Assert.True(deleteBody.Data);
        repository.VerifyAll();
    }

    private static PayrollPoliciesController Controller(IPolicyRepository repository)
    {
        var controller = new PayrollPoliciesController(repository, NullLogger<PayrollPoliciesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "payroll-admin")
                    }, "test"))
                }
            }
        };
        return controller;
    }
}
