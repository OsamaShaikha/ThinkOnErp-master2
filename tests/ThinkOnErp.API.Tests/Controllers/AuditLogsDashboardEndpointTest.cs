using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Integration test for the dashboard counters endpoint.
/// Verifies that GET /api/auditlogs/dashboard returns the correct structure.
/// </summary>
public class AuditLogsDashboardEndpointTest : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditLogsDashboardEndpointTest(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetDashboardCounters_ReturnsCorrectStructure()
    {
        // Arrange - Get authentication token
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "superadmin",
            password = "SuperAdmin@123"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(loginResult);
        Assert.True(loginResult.Success);

        // Extract token from response
        var tokenProperty = loginResult.Data?.GetType().GetProperty("token");
        var token = tokenProperty?.GetValue(loginResult.Data)?.ToString();
        Assert.NotNull(token);

        // Add authorization header
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act - Call the dashboard endpoint
        var response = await _client.GetAsync("/api/auditlogs/legacy-dashboard");

        // Assert - Verify response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LegacyDashboardCounters>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // Verify the structure contains all required fields
        Assert.True(result.Data.UnresolvedCount >= 0);
        Assert.True(result.Data.InProgressCount >= 0);
        Assert.True(result.Data.ResolvedCount >= 0);
        Assert.True(result.Data.CriticalErrorsCount >= 0);

        // Log the actual values for verification
        Console.WriteLine($"Dashboard Counters:");
        Console.WriteLine($"  Unresolved: {result.Data.UnresolvedCount}");
        Console.WriteLine($"  In Progress: {result.Data.InProgressCount}");
        Console.WriteLine($"  Resolved: {result.Data.ResolvedCount}");
        Console.WriteLine($"  Critical Errors: {result.Data.CriticalErrorsCount}");
    }

    [Fact]
    public async Task GetDashboardCounters_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act - Call the dashboard endpoint without authentication
        var response = await _client.GetAsync("/api/auditlogs/legacy-dashboard");

        // Assert - Should return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
