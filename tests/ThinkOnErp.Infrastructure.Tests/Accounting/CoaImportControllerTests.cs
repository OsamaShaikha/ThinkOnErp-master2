using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Moq;
using Swashbuckle.AspNetCore.Swagger;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class CoaImportControllerTests
{
    private readonly Mock<ICoaExcelImportService> _service = new(MockBehavior.Strict);

    [Fact]
    public async Task Validate_WhenWorkbookHasValidationErrors_Returns200WithStructuredResult()
    {
        var validation = new CoaImportResultDto
        {
            TotalRows = 314,
            Errors = new List<CoaImportErrorDto>
            {
                new(null, null, "COA_ACCOUNT_COUNT", "Expected 315 accounts.")
            }
        };
        _service
            .Setup(service => service.ValidateAsync(
                It.Is<Stream>(stream => stream.CanRead),
                CancellationToken.None))
            .ReturnsAsync(validation);

        var action = await CreateController().Validate(
            CreateFile("coa.xlsx"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(validation, response.Data);
        Assert.False(response.Data!.IsValid);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Import_WhenWorkbookIsValid_Returns201()
    {
        var importResult = new CoaImportResultDto
        {
            TotalRows = 315,
            ImportedCount = 315
        };
        _service
            .Setup(service => service.ImportAsync(
                It.Is<Stream>(stream => stream.CanRead),
                9,
                CancellationToken.None))
            .ReturnsAsync(importResult);

        var action = await CreateController().Import(
            CreateFile("coa.xlsx"),
            9,
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(created.Value);
        Assert.True(response.Success);
        Assert.Equal(315, response.Data!.ImportedCount);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Import_WhenChartAlreadyExists_Returns409AndPreservesErrors()
    {
        var importResult = new CoaImportResultDto
        {
            TotalRows = 315,
            Errors = new List<CoaImportErrorDto>
            {
                new(
                    null,
                    null,
                    "COA_ALREADY_INITIALIZED",
                    "The current company already has a chart of accounts.")
            }
        };
        _service
            .Setup(service => service.ImportAsync(
                It.IsAny<Stream>(),
                9,
                CancellationToken.None))
            .ReturnsAsync(importResult);

        var action = await CreateController().Import(
            CreateFile("coa.xlsx"),
            9,
            CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(409, conflict.StatusCode);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(conflict.Value);
        Assert.False(response.Success);
        Assert.Same(importResult, response.Data);
        Assert.Contains("COA_ALREADY_INITIALIZED", Assert.Single(response.Errors!));
        _service.VerifyAll();
    }

    [Fact]
    public async Task Validate_WhenExtensionIsNotXlsx_Returns400WithoutCallingService()
    {
        var action = await CreateController().Validate(
            CreateFile("coa.xls"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Validate_WhenFileIsMissing_Returns400WithoutCallingService()
    {
        var action = await CreateController().Validate(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Validate_WhenFileExceeds10Mb_Returns400WithoutCallingService()
    {
        var file = new FormFile(
            new MemoryStream(new byte[] { 1 }),
            0,
            (10L * 1024 * 1024) + 1,
            "file",
            "coa.xlsx");

        var action = await CreateController().Validate(file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Import_WhenDefaultBranchIdIsInvalid_Returns400WithoutCallingService()
    {
        var action = await CreateController().Import(
            CreateFile("coa.xlsx"),
            0,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<CoaImportResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public void Controller_UsesTenantScopeAndTenantAdminPolicy()
    {
        var controllerType = typeof(CoaImportController);

        Assert.NotNull(Attribute.GetCustomAttribute(controllerType, typeof(TenantScopedAttribute)));
        var authorize = Assert.Single(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        Assert.Equal("TenantAdminOnly", authorize.Policy);
    }

    [Fact]
    public void SwaggerGeneration_AccountingDocumentContainsOnlyAccountingRoutes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = new Mock<IWebHostEnvironment>();
        environment
            .SetupGet(item => item.ApplicationName)
            .Returns(typeof(CoaImportController).Assembly.GetName().Name!);
        environment.SetupGet(item => item.EnvironmentName).Returns("Development");
        services.AddSingleton<IWebHostEnvironment>(environment.Object);
        services.AddSingleton<IHostEnvironment>(environment.Object);
        services
            .AddControllers()
            .AddApplicationPart(typeof(CoaImportController).Assembly);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(ApiSwaggerDocuments.Accounting, new OpenApiInfo
            {
                Title = "Accounting API",
                Version = "v1"
            });
            options.SwaggerDoc(ApiSwaggerDocuments.Company, new OpenApiInfo
            {
                Title = "Company API",
                Version = "v1"
            });
            options.SwaggerDoc(ApiSwaggerDocuments.SuperAdmin, new OpenApiInfo
            {
                Title = "SuperAdmin API",
                Version = "v1"
            });
            options.DocInclusionPredicate((documentName, apiDescription) =>
                ApiSwaggerDocuments.Includes(
                    documentName,
                    apiDescription.ActionDescriptor.RouteValues["controller"],
                    apiDescription.RelativePath));
            options.OperationFilter<TenantCompanyHeaderOperationFilter>();
        });

        using var provider = services.BuildServiceProvider();
        var swagger = provider.GetRequiredService<ISwaggerProvider>();
        var accountingDocument = swagger.GetSwagger(ApiSwaggerDocuments.Accounting);
        var companyDocument = swagger.GetSwagger(ApiSwaggerDocuments.Company);
        var superAdminDocument = swagger.GetSwagger(ApiSwaggerDocuments.SuperAdmin);

        Assert.Contains("/api/accounting/coa-import/validate", accountingDocument.Paths.Keys);
        Assert.Contains("/api/accounting/coa-import/import", accountingDocument.Paths.Keys);
        Assert.Contains("/api/accounting/gl-accounts/tree", accountingDocument.Paths.Keys);
        Assert.Contains("/api/accounting/gl-accounts/postable", accountingDocument.Paths.Keys);
        Assert.Contains("/api/accounting/gl-accounts/{accountCode}", accountingDocument.Paths.Keys);
        Assert.Contains("/api/accounting/gl-accounts/{accountCode}/status", accountingDocument.Paths.Keys);
        Assert.Contains("/api/fiscalyears", accountingDocument.Paths.Keys);
        Assert.Contains("/api/fiscalyears/{id}/close", accountingDocument.Paths.Keys);
        Assert.Contains("/api/currencies", accountingDocument.Paths.Keys);
        Assert.DoesNotContain("/api/Auth/login", accountingDocument.Paths.Keys);
        Assert.DoesNotContain("/api/tickets", accountingDocument.Paths.Keys);
        Assert.DoesNotContain("/api/documents", accountingDocument.Paths.Keys);

        Assert.Contains("/api/accounting/gl-accounts/tree", companyDocument.Paths.Keys);
        Assert.Contains("/api/Auth/login", companyDocument.Paths.Keys);
        Assert.DoesNotContain("/api/currencies", companyDocument.Paths.Keys);
        Assert.Contains("/api/documents/metadata", companyDocument.Paths.Keys);

        Assert.Contains("/api/currencies", superAdminDocument.Paths.Keys);
        Assert.Contains("/api/health", superAdminDocument.Paths.Keys);
        Assert.Contains("/api/documents/metadata", superAdminDocument.Paths.Keys);
        Assert.DoesNotContain(
            "/api/accounting/gl-accounts/tree",
            superAdminDocument.Paths.Keys);

        var treeOperation = accountingDocument.Paths["/api/accounting/gl-accounts/tree"]
            .Operations[OperationType.Get];
        Assert.Contains(treeOperation.Parameters, parameter => parameter.Name == "X-Company-Code");
        Assert.Contains(treeOperation.Parameters, parameter => parameter.Name == "X-Company-Id");

        var accountPath = accountingDocument.Paths["/api/accounting/gl-accounts/{accountCode}"];
        Assert.Contains(OperationType.Get, accountPath.Operations.Keys);
        Assert.Contains(OperationType.Put, accountPath.Operations.Keys);
        Assert.Contains(OperationType.Delete, accountPath.Operations.Keys);
        Assert.Contains("404", accountPath.Operations[OperationType.Get].Responses.Keys);
        Assert.Contains("409", accountPath.Operations[OperationType.Delete].Responses.Keys);

        var currencyOperation = accountingDocument.Paths["/api/currencies"]
            .Operations[OperationType.Get];
        Assert.DoesNotContain(
            currencyOperation.Parameters ?? new List<OpenApiParameter>(),
            parameter => parameter.Name is "X-Company-Code" or "X-Company-Id");
    }

    private CoaImportController CreateController() => new(
        _service.Object,
        Mock.Of<ILogger<CoaImportController>>());

    private static FormFile CreateFile(string fileName)
    {
        var content = new byte[] { 1, 2, 3, 4 };
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}
