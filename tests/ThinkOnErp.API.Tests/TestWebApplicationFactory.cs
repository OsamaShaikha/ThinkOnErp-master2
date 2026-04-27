using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;

namespace ThinkOnErp.API.Tests;

/// <summary>
/// Test web application factory for integration tests
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

    /// <summary>
    /// Helper method to get an admin authentication token for testing
    /// </summary>
    public async Task<string> GetAdminTokenAsync()
    {
        var client = CreateClient();
        
        var loginRequest = new LoginDto
        {
            UserName = "admin",
            Password = "admin123"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get admin token. Status: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        
        if (result?.Data?.AccessToken == null)
        {
            throw new Exception("Login response did not contain an access token");
        }

        return result.Data.AccessToken;
    }
}
