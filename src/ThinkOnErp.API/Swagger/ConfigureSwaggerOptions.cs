using System.Linq;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace ThinkOnErp.API.Swagger;

/// <summary>
/// Configures SwaggerGenOptions and SwaggerUIOptions dynamically by reading categories from Oracle DB.
/// </summary>
public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>, IConfigureOptions<SwaggerUIOptions>
{
    private readonly SwaggerCategoryLoader _loader;

    public ConfigureSwaggerOptions(SwaggerCategoryLoader loader)
    {
        _loader = loader;
    }

    public void Configure(SwaggerGenOptions options)
    {
        var categories = _loader.GetCategories();
        foreach (var cat in categories)
        {
            options.SwaggerDoc(cat.CategoryCode, new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = cat.DisplayTitle,
                Version = "v1.0",
                Description = cat.Description
            });
        }

        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            var controller = apiDesc.ActionDescriptor.RouteValues["controller"];
            return _loader.Includes(docName, apiDesc.GroupName, controller, apiDesc.RelativePath, apiDesc.HttpMethod);
        });
    }

    public void Configure(SwaggerUIOptions options)
    {
        var categories = _loader.GetCategories().OrderBy(c => c.DisplayOrder);
        foreach (var cat in categories)
        {
            options.SwaggerEndpoint($"/swagger/{cat.CategoryCode}/swagger.json", cat.DisplayTitle);
        }
    }
}
