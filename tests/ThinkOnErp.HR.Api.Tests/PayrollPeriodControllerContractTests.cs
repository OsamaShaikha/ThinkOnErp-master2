using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollPeriodControllerContractTests
{
    [Fact]
    public async Task CreatePeriod_UsesTrustedTenantAndServerOwnedFields()
    {
        var payroll = new Mock<IPayrollService>();
        PayrollPeriod? saved = null;
        payroll.Setup(s => s.CreatePeriodAsync(It.IsAny<PayrollPeriod>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollPeriod, CancellationToken>((period, _) => saved = period)
            .ReturnsAsync((PayrollPeriod period, CancellationToken _) => period);
        var controller = new PayrollController(payroll.Object, Mock.Of<IPayrollExplanationService>(), NullLogger<PayrollController>.Instance);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "period-creator") }, "TestAuth"))
        };
        http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(42, "C42", "TENANT_42");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        var dto = new CreatePayrollPeriodDto("2026-11", "تشرين الثاني", "November", 2026, 11,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), new DateTime(2026, 12, 1), "monthly");

        var result = await controller.CreatePeriod(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(saved);
        Assert.Equal(42, saved!.CompanyId);
        Assert.Equal("OPEN", saved.Status);
        Assert.Equal(0, saved.Id);
        Assert.Equal("period-creator", saved.CreationUser);
        Assert.Equal("MONTHLY", saved.PayrollType);
    }
}
