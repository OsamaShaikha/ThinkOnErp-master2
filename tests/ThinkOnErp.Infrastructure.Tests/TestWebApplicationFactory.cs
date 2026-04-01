using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.API;

namespace ThinkOnErp.Infrastructure.Tests;

/// <summary>
/// Test web application factory for infrastructure integration tests
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Use test configuration
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:OracleDb"] = "Data Source=localhost:1521/XEPDB1;User Id=THINKONERP;Password=oracle123;",
                ["JwtSettings:SecretKey"] = "ThisIsAVerySecretKeyForTestingPurposesOnly123456789",
                ["JwtSettings:Issuer"] = "ThinkOnErpAPI",
                ["JwtSettings:Audience"] = "ThinkOnErpClient",
                ["JwtSettings:ExpiryInMinutes"] = "60"
            }!);
        });

        builder.ConfigureServices(services =>
        {
            // Additional test-specific service configuration can go here
        });
    }
}
