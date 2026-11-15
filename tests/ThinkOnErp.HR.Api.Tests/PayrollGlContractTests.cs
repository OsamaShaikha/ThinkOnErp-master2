using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Application;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public class PayrollGlContractTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reason\":\"Correction\"}")]
    [InlineData("{\"reversalDate\":\"2027-01-05\"}")]
    public void ReversalRequiresExplicitDateAndReasonInJson(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReversePayrollGlDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public async Task LegacyFinancialRoutesReturnConflictWithoutCallingHrService()
    {
        var request = new ReversePayrollGlDto(new DateTime(2027, 1, 5), "Correction");
        var service = new Mock<IPayrollService>(MockBehavior.Strict);
        var controller = new PayrollController(service.Object, Mock.Of<IPayrollExplanationService>(), NullLogger<PayrollController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<ConflictObjectResult>((await controller.ReverseGl(7, request, CancellationToken.None)).Result);
        Assert.IsType<ConflictObjectResult>((await controller.PostToGl(7, CancellationToken.None)).Result);
        Assert.IsType<ConflictObjectResult>(await controller.ExportBankFile(7, "CSV", CancellationToken.None));
        Assert.IsType<ConflictObjectResult>((await controller.PreviewBankExport(7, "CSV", CancellationToken.None)).Result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyHrPostingConfigurationCannotWriteAccountingSettings()
    {
        var service = new Mock<IHrPayrollPostingService>(MockBehavior.Strict);
        var controller = new PayrollPostingConfigurationsController(service.Object);
        Assert.IsType<ConflictObjectResult>(await controller.Save(7, new PayrollPostingConfiguration { BranchId = 7 }, CancellationToken.None));
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public void LiveApplicationDoesNotRegisterLegacyFinancialEngineOrBankExporter()
    {
        var services = new ServiceCollection(); services.AddApplication();
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IBankPayrollExportService));
        Assert.DoesNotContain(services, s => s.ImplementationType?.Namespace == "ThinkOnErp.Application.Services.Accounting.Legacy");
        Assert.Contains(services, s => s.ServiceType == typeof(IPayrollService) && s.ImplementationType == typeof(PayrollService));
    }
}
