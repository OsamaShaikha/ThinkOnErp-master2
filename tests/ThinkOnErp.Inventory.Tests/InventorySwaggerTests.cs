using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using ThinkOnErp.API.Controllers.Inventory;
using ThinkOnErp.API.Swagger;
using Xunit;
using Microsoft.AspNetCore.Builder;

namespace ThinkOnErp.Inventory.Tests;

public sealed class InventorySwaggerTests
{
    [Fact]
    public void InventoryDefinitionGeneratesWithMultipartFileUploads()
    {
        var services = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" }).Services;
        services.AddLogging();
        services.AddControllers().AddApplicationPart(typeof(InventoryExcelController).Assembly);
        services.AddSwaggerGen(options => options.SwaggerDoc(ApiCategories.Inventory, new OpenApiInfo { Title = "Inventory", Version = "v1" }));
        using var provider = services.BuildServiceProvider();
        var document = provider.GetRequiredService<ISwaggerProvider>().GetSwagger(ApiCategories.Inventory);
        foreach (var resource in new[] { "items", "categories" })
        {
            Assert.Contains($"/api/inventory/{resource}/export", document.Paths.Keys);
            var upload = document.Paths[$"/api/inventory/{resource}/import"].Operations[OperationType.Post];
            var schema = upload.RequestBody.Content["multipart/form-data"].Schema;
            Assert.Equal("binary", schema.Properties["file"].Format);
            Assert.Contains(upload.Parameters, p => p.Name == "branchId" && p.In == ParameterLocation.Query);
        }
    }
}
