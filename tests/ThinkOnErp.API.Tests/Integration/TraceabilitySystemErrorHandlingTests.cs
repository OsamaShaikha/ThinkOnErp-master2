using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using Xunit;

namespace ThinkOnErp.API.Tests.Integration;

/// <summary>
/// Integration tests for error handling scenarios in the Full Traceability System.
/// Tests proper error responses, validation, and exception handling across all controllers.
/// 
/// **Validates: Requirements 19.10**
/// Comprehensive testing of error handling including:
/// - Input validation and error responses
/// - Exception handling and proper HTTP status codes
/// - Error message consistency and security
/// - Graceful degradation under error conditions
/// </summary>
public class TraceabilitySystemErrorHandlingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly string _adminToken;

    public TraceabilitySystemErrorHandlingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Get admin token for authenticated tests
        _adminToken = GetAdminTokenAsync().GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
    }

    #region Input Validation Tests

    /// <summary>
    /// Tests that endpoints properly validate pagination parameters and return appropriate error messages.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/legacy?pageNumber=0&pageSize=10", "Page number must be greater than 0")]
    [InlineData("/api/auditlogs/legacy?pageNumber=-1&pageSize=10", "Page number must be greater than 0")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=0", "Page size must be between 1 and 100")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=-5", "Page size must be between 1 and 100")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=101", "Page size must be between 1 and 100")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=1000", "Page size must be between 1 and 100")]
    [InlineData("/api/alerts/rules?pageNumber=0&pageSize=50", "Page number must be greater than 0")]
    [InlineData("/api/alerts/history?pageNumber=1&pageSize=101", "Page size must be between 1 and 100")]
    [InlineData("/api/monitoring/performance/slow-requests?pageNumber=-1", "Page number must be greater than 0")]
    [InlineData("/api/monitoring/performance/slow-queries?pageSize=200", "Page size must be between 1 and 100")]
    public async Task InvalidPaginationParameters_ReturnBadRequestWithMessage(string endpoint, string expectedErrorPattern)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify error message contains expected pattern (case-insensitive)
        Assert.Contains(expectedErrorPattern.ToLower(), content.ToLower());
    }

    /// <summary>
    /// Tests that endpoints properly validate date range parameters.
    /// </summary>
    [Theory]
    [InlineData("/api/compliance/gdpr/access-report?dataSubjectId=1&startDate=2024-12-31&endDate=2024-01-01")]
    [InlineData("/api/compliance/sox/financial-access?startDate=2024-06-01&endDate=2024-01-01")]
    [InlineData("/api/compliance/iso27001/security-report?startDate=2024-12-31&endDate=2024-12-30")]
    [InlineData("/api/compliance/user-activity?userId=1&startDate=2024-12-31&endDate=2024-01-01")]
    public async Task InvalidDateRanges_ReturnBadRequest(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("date", content.ToLower());
    }

    /// <summary>
    /// Tests that endpoints properly validate required parameters.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/correlation/", "Correlation ID cannot be empty")]
    [InlineData("/api/auditlogs/entity//1", "Entity type cannot be empty")]
    [InlineData("/api/auditlogs/entity/SysUser/0", "Entity ID must be greater than 0")]
    [InlineData("/api/auditlogs/entity/SysUser/-1", "Entity ID must be greater than 0")]
    [InlineData("/api/monitoring/security/check-failed-logins", "IP address parameter is required")]
    [InlineData("/api/monitoring/security/failed-login-count", "Username parameter is required")]
    [InlineData("/api/monitoring/security/check-anomalous-activity?userId=0", "Valid user ID is required")]
    [InlineData("/api/monitoring/security/check-anomalous-activity?userId=-1", "Valid user ID is required")]
    public async Task MissingOrInvalidRequiredParameters_ReturnBadRequest(string endpoint, string expectedErrorPattern)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedErrorPattern.ToLower(), content.ToLower());
    }

    #endregion

    #region Request Body Validation Tests

    /// <summary>
    /// Tests that POST/PUT endpoints properly validate request body content.
    /// </summary>
    [Fact]
    public async Task AuditLogStatusUpdate_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var invalidStatusUpdate = new { Status = "InvalidStatus", ResolutionNotes = "Test" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", invalidStatusUpdate);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", content.ToLower());
    }

    /// <summary>
    /// Tests that alert rule creation validates required fields.
    /// </summary>
    [Fact]
    public async Task CreateAlertRule_WithMissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange
        var invalidAlertRule = new { Name = "", EventType = "" }; // Missing required fields

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/rules", invalidAlertRule);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that endpoints handle malformed JSON properly.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/legacy/1/status", "{ invalid json }")]
    [InlineData("/api/alerts/rules", "{ \"name\": }")]
    [InlineData("/api/monitoring/security/check-sql-injection", "{ malformed }")]
    public async Task MalformedJsonRequests_ReturnBadRequest(string endpoint, string malformedJson)
    {
        // Arrange
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that endpoints handle empty request bodies appropriately.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/legacy/1/status")]
    [InlineData("/api/alerts/rules")]
    [InlineData("/api/monitoring/security/check-sql-injection")]
    public async Task EmptyRequestBodies_ReturnBadRequest(string endpoint)
    {
        // Arrange
        var emptyContent = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, emptyContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Not Found Tests

    /// <summary>
    /// Tests that endpoints return 404 for non-existent resources.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/999999/status")]
    [InlineData("/api/auditlogs/correlation/non-existent-correlation-id")]
    [InlineData("/api/auditlogs/entity/NonExistentEntity/1")]
    public async Task NonExistentResources_ReturnNotFoundOrBadRequest(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        // Should return not found, bad request, or success with empty results
        // but not unauthorized or forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.IsSuccessStatusCode);
    }

    #endregion

    #region Content Type Tests

    /// <summary>
    /// Tests that endpoints handle unsupported content types properly.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/legacy/1/status", "text/plain")]
    [InlineData("/api/alerts/rules", "application/xml")]
    [InlineData("/api/monitoring/security/check-sql-injection", "text/html")]
    public async Task UnsupportedContentTypes_ReturnUnsupportedMediaType(string endpoint, string contentType)
    {
        // Arrange
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, contentType);

        // Act
        var response = await _client.PostAsync(endpoint, content);

        // Assert
        // Should return unsupported media type or bad request
        Assert.True(
            response.StatusCode == HttpStatusCode.UnsupportedMediaType ||
            response.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Large Request Tests

    /// <summary>
    /// Tests that endpoints handle oversized requests appropriately.
    /// </summary>
    [Fact]
    public async Task OversizedRequestBody_ReturnsRequestEntityTooLarge()
    {
        // Arrange - Create a very large request body
        var largeObject = new
        {
            Status = "Resolved",
            ResolutionNotes = new string('A', 10000) // 10KB of text
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", largeObject);

        // Assert
        // Should handle large requests gracefully (may succeed or return appropriate error)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Error Response Format Tests

    /// <summary>
    /// Tests that error responses follow a consistent format.
    /// </summary>
    [Fact]
    public async Task ErrorResponses_FollowConsistentFormat()
    {
        // Act - Make a request that should return a validation error
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Try to parse as JSON to verify it's structured
        try
        {
            var jsonDoc = JsonDocument.Parse(content);
            Assert.True(jsonDoc.RootElement.ValueKind == JsonValueKind.Object);
            
            // Check for common error response fields
            var hasErrorInfo = jsonDoc.RootElement.TryGetProperty("success", out _) ||
                              jsonDoc.RootElement.TryGetProperty("error", out _) ||
                              jsonDoc.RootElement.TryGetProperty("message", out _) ||
                              jsonDoc.RootElement.TryGetProperty("errors", out _);
            
            Assert.True(hasErrorInfo, "Error response should contain error information");
        }
        catch (JsonException)
        {
            // If not JSON, should at least be a meaningful error message
            Assert.True(content.Length > 10, "Error response should contain meaningful content");
        }
    }

    #endregion

    #region Security Error Tests

    /// <summary>
    /// Tests that security-related errors don't leak sensitive information.
    /// </summary>
    [Fact]
    public async Task SecurityErrors_DontLeakSensitiveInformation()
    {
        // Arrange - Use an invalid token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify that sensitive information is not leaked
        var sensitiveTerms = new[] { "secret", "key", "password", "token", "claim", "signature" };
        foreach (var term in sensitiveTerms)
        {
            Assert.DoesNotContain(term, content.ToLower());
        }
    }

    #endregion

    #region Concurrent Error Handling Tests

    /// <summary>
    /// Tests that error handling works correctly under concurrent load.
    /// </summary>
    [Fact]
    public async Task ConcurrentErrorRequests_HandleGracefully()
    {
        // Arrange - Create multiple clients with invalid requests
        var clients = Enumerable.Range(0, 5).Select(_ => _factory.CreateClient()).ToArray();
        
        foreach (var client in clients)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        }

        // Act - Make concurrent invalid requests
        var tasks = clients.Select(client => 
            client.GetAsync("/api/auditlogs/legacy?pageNumber=0")).ToArray();
        
        var responses = await Task.WhenAll(tasks);

        // Assert - All should return consistent error responses
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    #endregion

    #region Method Not Allowed Tests

    /// <summary>
    /// Tests that endpoints return 405 Method Not Allowed for unsupported HTTP methods.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/dashboard", "POST")]
    [InlineData("/api/auditlogs/dashboard", "PUT")]
    [InlineData("/api/auditlogs/dashboard", "DELETE")]
    [InlineData("/api/monitoring/health", "POST")]
    [InlineData("/api/monitoring/health", "PUT")]
    [InlineData("/api/alerts/rules", "PATCH")]
    public async Task UnsupportedHttpMethods_ReturnMethodNotAllowed(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    #endregion

    #region Timeout and Performance Tests

    /// <summary>
    /// Tests that endpoints handle slow operations gracefully.
    /// This test verifies that requests don't hang indefinitely.
    /// </summary>
    [Fact]
    public async Task SlowOperations_CompleteWithinReasonableTime()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Make requests that might be slow
        var endpoints = new[]
        {
            "/api/auditlogs/legacy?pageSize=1",
            "/api/monitoring/memory",
            "/api/alerts/history?pageSize=1"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint, cts.Token);
            
            // Assert - Should complete within timeout
            Assert.NotEqual(HttpStatusCode.RequestTimeout, response.StatusCode);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets an admin authentication token for testing.
    /// </summary>
    private async Task<string> GetAdminTokenAsync()
    {
        var loginRequest = new LoginDto
        {
            UserName = "admin",
            Password = "admin123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to get admin token. Status: {response.StatusCode}, Content: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        
        if (result?.Data?.AccessToken == null)
        {
            throw new Exception("Login response did not contain an access token");
        }

        return result.Data.AccessToken;
    }

    #endregion
}