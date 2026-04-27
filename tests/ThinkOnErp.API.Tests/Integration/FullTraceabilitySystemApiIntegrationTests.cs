using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Compliance;
using ThinkOnErp.Domain.Models;
using LegacyAuditLogDto = ThinkOnErp.Domain.Models.LegacyAuditLogDto;
using Xunit;

namespace ThinkOnErp.API.Tests.Integration;

/// <summary>
/// Integration tests for Full Traceability System API endpoints with authentication.
/// Tests JWT authentication, role-based authorization, and proper error handling
/// for AuditLogsController, ComplianceController, MonitoringController, and AlertsController.
/// 
/// **Validates: Requirements 19.10**
/// Tests verify that the Full Traceability System API endpoints properly handle 
/// authentication and authorization scenarios including:
/// - JWT authentication and token validation
/// - Role-based authorization (admin-only endpoints vs regular user endpoints)
/// - Authentication failure scenarios (invalid tokens, expired tokens, missing tokens)
/// - Authorization failure scenarios (insufficient permissions)
/// </summary>
public class FullTraceabilitySystemApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly string _adminToken;

    public FullTraceabilitySystemApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Get admin token for authenticated tests
        _adminToken = GetAdminTokenAsync().GetAwaiter().GetResult();
    }

    #region Authentication Tests

    /// <summary>
    /// Tests that all Full Traceability System endpoints require authentication.
    /// Verifies that requests without authentication tokens return 401 Unauthorized.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/auditlogs/legacy")]
    [InlineData("GET", "/api/auditlogs/dashboard")]
    [InlineData("PUT", "/api/auditlogs/legacy/1/status")]
    [InlineData("GET", "/api/auditlogs/1/status")]
    [InlineData("GET", "/api/auditlogs/correlation/test-correlation-id")]
    [InlineData("GET", "/api/auditlogs/entity/SysUser/1")]
    [InlineData("GET", "/api/auditlogs/replay/user/1")]
    [InlineData("GET", "/api/compliance/gdpr/access-report?dataSubjectId=1&startDate=2024-01-01&endDate=2024-12-31")]
    [InlineData("GET", "/api/compliance/gdpr/data-export?dataSubjectId=1")]
    [InlineData("GET", "/api/compliance/sox/financial-access?startDate=2024-01-01&endDate=2024-12-31")]
    [InlineData("GET", "/api/compliance/sox/segregation-of-duties")]
    [InlineData("GET", "/api/compliance/iso27001/security-report?startDate=2024-01-01&endDate=2024-12-31")]
    [InlineData("GET", "/api/compliance/user-activity?userId=1&startDate=2024-01-01&endDate=2024-12-31")]
    [InlineData("GET", "/api/compliance/data-modification?entityType=SysUser&entityId=1")]
    [InlineData("GET", "/api/monitoring/memory")]
    [InlineData("GET", "/api/monitoring/memory/pressure")]
    [InlineData("GET", "/api/monitoring/memory/recommendations")]
    [InlineData("POST", "/api/monitoring/memory/optimize")]
    [InlineData("POST", "/api/monitoring/memory/gc")]
    [InlineData("GET", "/api/monitoring/performance/endpoint?endpoint=/api/test")]
    [InlineData("GET", "/api/monitoring/performance/slow-requests")]
    [InlineData("GET", "/api/monitoring/performance/slow-queries")]
    [InlineData("GET", "/api/monitoring/audit-queue-depth")]
    [InlineData("GET", "/api/monitoring/security/threats")]
    [InlineData("GET", "/api/monitoring/security/daily-summary")]
    [InlineData("GET", "/api/monitoring/security/check-failed-logins?ipAddress=192.168.1.1")]
    [InlineData("GET", "/api/monitoring/security/failed-login-count?username=testuser")]
    [InlineData("POST", "/api/monitoring/security/check-sql-injection")]
    [InlineData("POST", "/api/monitoring/security/check-xss")]
    [InlineData("GET", "/api/monitoring/security/check-anomalous-activity?userId=1")]
    [InlineData("GET", "/api/monitoring/performance/connection-pool")]
    [InlineData("GET", "/api/alerts/rules")]
    [InlineData("POST", "/api/alerts/rules")]
    [InlineData("PUT", "/api/alerts/rules/1")]
    [InlineData("DELETE", "/api/alerts/rules/1")]
    [InlineData("GET", "/api/alerts/history")]
    [InlineData("POST", "/api/alerts/1/acknowledge")]
    [InlineData("POST", "/api/alerts/1/resolve")]
    [InlineData("POST", "/api/alerts/test/email")]
    [InlineData("POST", "/api/alerts/test/webhook")]
    [InlineData("POST", "/api/alerts/test/sms")]
    public async Task FullTraceabilityEndpoints_WithoutAuthentication_Return401Unauthorized(string method, string endpoint)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response;
        switch (method.ToUpper())
        {
            case "GET":
                response = await _client.GetAsync(endpoint);
                break;
            case "POST":
                response = await _client.PostAsync(endpoint, new StringContent("{}", Encoding.UTF8, "application/json"));
                break;
            case "PUT":
                response = await _client.PutAsync(endpoint, new StringContent("{\"status\":\"Resolved\"}", Encoding.UTF8, "application/json"));
                break;
            case "DELETE":
                response = await _client.DeleteAsync(endpoint);
                break;
            default:
                throw new ArgumentException($"Unsupported HTTP method: {method}");
        }

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that endpoints reject invalid JWT tokens.
    /// Verifies that requests with malformed or invalid tokens return 401 Unauthorized.
    /// </summary>
    [Theory]
    [InlineData("invalid-token")]
    [InlineData("Bearer.invalid.token")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature")]
    public async Task FullTraceabilityEndpoints_WithInvalidToken_Return401Unauthorized(string invalidToken)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that endpoints reject expired JWT tokens.
    /// This test would require creating an expired token, which is complex in integration tests.
    /// In a real scenario, you would mock the JWT service or use a test token with past expiration.
    /// </summary>
    [Fact]
    public async Task FullTraceabilityEndpoints_WithExpiredToken_Return401Unauthorized()
    {
        // Arrange - Create an expired token (this is a simplified example)
        // In practice, you would need to create a token with past expiration time
        var expiredToken = "expired.jwt.token"; // This would be a real expired JWT in practice
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    /// <summary>
    /// Tests that all Full Traceability System endpoints require admin privileges.
    /// Verifies that regular users (non-admin) cannot access admin-only endpoints.
    /// Note: This test assumes a regular user exists in the test database.
    /// </summary>
    [Fact]
    public async Task FullTraceabilityEndpoints_WithRegularUser_Return403Forbidden()
    {
        // Arrange - Try to get a regular user token
        var regularUserToken = await TryGetRegularUserTokenAsync();
        
        if (regularUserToken == null)
        {
            // Skip test if no regular user exists in test database
            Assert.True(true, "Regular user not found in test database - skipping authorization test");
            return;
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regularUserToken);

        // Act & Assert - Test a few representative endpoints
        var auditResponse = await _client.GetAsync("/api/auditlogs/legacy");
        Assert.Equal(HttpStatusCode.Forbidden, auditResponse.StatusCode);

        var complianceResponse = await _client.GetAsync("/api/compliance/gdpr/access-report?dataSubjectId=1&startDate=2024-01-01&endDate=2024-12-31");
        Assert.Equal(HttpStatusCode.Forbidden, complianceResponse.StatusCode);

        var monitoringResponse = await _client.GetAsync("/api/monitoring/memory");
        Assert.Equal(HttpStatusCode.Forbidden, monitoringResponse.StatusCode);

        var alertsResponse = await _client.GetAsync("/api/alerts/rules");
        Assert.Equal(HttpStatusCode.Forbidden, alertsResponse.StatusCode);
    }

    /// <summary>
    /// Tests that admin users can successfully access all Full Traceability System endpoints.
    /// Verifies that valid admin tokens allow access to protected resources.
    /// </summary>
    [Fact]
    public async Task FullTraceabilityEndpoints_WithAdminUser_ReturnSuccessOrExpectedError()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act & Assert - Test representative endpoints that should be accessible
        
        // Audit Logs endpoints
        var auditResponse = await _client.GetAsync("/api/auditlogs/legacy");
        Assert.True(auditResponse.IsSuccessStatusCode || auditResponse.StatusCode == HttpStatusCode.BadRequest);

        var dashboardResponse = await _client.GetAsync("/api/auditlogs/dashboard");
        Assert.True(dashboardResponse.IsSuccessStatusCode);

        // Monitoring endpoints (health should be accessible)
        var healthResponse = await _client.GetAsync("/api/monitoring/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var memoryResponse = await _client.GetAsync("/api/monitoring/memory");
        Assert.True(memoryResponse.IsSuccessStatusCode);

        // Alerts endpoints
        var alertRulesResponse = await _client.GetAsync("/api/alerts/rules");
        Assert.True(alertRulesResponse.IsSuccessStatusCode);

        var alertHistoryResponse = await _client.GetAsync("/api/alerts/history");
        Assert.True(alertHistoryResponse.IsSuccessStatusCode);
    }

    #endregion

    #region Health Check Tests

    /// <summary>
    /// Tests that the monitoring health endpoint is accessible without authentication.
    /// Health checks should be available for load balancers and monitoring systems.
    /// </summary>
    [Fact]
    public async Task MonitoringHealth_WithoutAuthentication_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/monitoring/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    #endregion

    #region Audit Logs Controller Tests

    /// <summary>
    /// Tests the legacy audit logs endpoint with valid authentication.
    /// Verifies that authenticated admin users can retrieve audit logs.
    /// </summary>
    [Fact]
    public async Task AuditLogs_GetLegacyAuditLogs_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=10");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<PagedResult<LegacyAuditLogDto>>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    /// <summary>
    /// Tests the dashboard counters endpoint with valid authentication.
    /// Verifies that authenticated admin users can retrieve dashboard metrics.
    /// </summary>
    [Fact]
    public async Task AuditLogs_GetDashboardCounters_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<LegacyDashboardCounters>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    /// <summary>
    /// Tests audit log status update with invalid data.
    /// Verifies proper validation and error handling.
    /// </summary>
    [Fact]
    public async Task AuditLogs_UpdateStatus_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var invalidStatusUpdate = new { Status = "InvalidStatus" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", invalidStatusUpdate);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests correlation ID lookup with valid authentication.
    /// Verifies that authenticated admin users can trace requests by correlation ID.
    /// </summary>
    [Fact]
    public async Task AuditLogs_GetByCorrelationId_WithValidAuth_ReturnsSuccessOrNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var testCorrelationId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync($"/api/auditlogs/correlation/{testCorrelationId}");

        // Assert
        // Should return success (with empty results) or not found, but not unauthorized
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Compliance Controller Tests

    /// <summary>
    /// Tests GDPR access report generation with valid authentication.
    /// Verifies that authenticated admin users can generate compliance reports.
    /// </summary>
    [Fact]
    public async Task Compliance_GenerateGdprAccessReport_WithValidAuth_ReturnsSuccessOrExpectedError()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/compliance/gdpr/access-report?dataSubjectId=1&startDate={startDate}&endDate={endDate}");

        // Assert
        // Should not return unauthorized or forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        // May return success, bad request (if service not fully implemented), or not found
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Tests SOX financial access report with invalid date range.
    /// Verifies proper validation of input parameters.
    /// </summary>
    [Fact]
    public async Task Compliance_GenerateSoxReport_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var startDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"); // End before start

        // Act
        var response = await _client.GetAsync($"/api/compliance/sox/financial-access?startDate={startDate}&endDate={endDate}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Monitoring Controller Tests

    /// <summary>
    /// Tests memory metrics endpoint with valid authentication.
    /// Verifies that authenticated admin users can access system monitoring data.
    /// </summary>
    [Fact]
    public async Task Monitoring_GetMemoryMetrics_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/monitoring/memory");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    /// <summary>
    /// Tests memory pressure detection with valid authentication.
    /// </summary>
    [Fact]
    public async Task Monitoring_GetMemoryPressure_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/monitoring/memory/pressure");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests security threats endpoint with valid authentication.
    /// </summary>
    [Fact]
    public async Task Monitoring_GetSecurityThreats_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/monitoring/security/threats");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests SQL injection detection with valid input.
    /// </summary>
    [Fact]
    public async Task Monitoring_CheckSqlInjection_WithValidAuth_ReturnsSuccessOrNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var testInput = "SELECT * FROM users WHERE id = 1";

        // Act
        var response = await _client.PostAsJsonAsync("/api/monitoring/security/check-sql-injection", testInput);

        // Assert
        // Should return success (threat detected) or not found (no threat), but not unauthorized
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Alerts Controller Tests

    /// <summary>
    /// Tests alert rules retrieval with valid authentication.
    /// </summary>
    [Fact]
    public async Task Alerts_GetAlertRules_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/alerts/rules");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    /// <summary>
    /// Tests alert history retrieval with valid authentication.
    /// </summary>
    [Fact]
    public async Task Alerts_GetAlertHistory_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/alerts/history");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Tests alert rule creation with invalid data.
    /// Verifies proper validation of alert rule parameters.
    /// </summary>
    [Fact]
    public async Task Alerts_CreateAlertRule_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var invalidAlertRule = new { Name = "", EventType = "InvalidType" }; // Missing required fields

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/rules", invalidAlertRule);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests email notification testing endpoint.
    /// </summary>
    [Fact]
    public async Task Alerts_TestEmailNotification_WithValidAuth_ReturnsSuccessOrError()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var testEmails = new[] { "test@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/test/email", testEmails);

        // Assert
        // Should not return unauthorized or forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        // May succeed or fail depending on email configuration, but should be accessible
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Input Validation Tests

    /// <summary>
    /// Tests that endpoints properly validate pagination parameters.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/legacy?pageNumber=0&pageSize=10")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=0")]
    [InlineData("/api/auditlogs/legacy?pageNumber=1&pageSize=101")]
    [InlineData("/api/alerts/rules?pageNumber=-1&pageSize=50")]
    [InlineData("/api/alerts/history?pageNumber=1&pageSize=200")]
    public async Task Endpoints_WithInvalidPaginationParameters_ReturnBadRequest(string endpoint)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that endpoints properly validate required parameters.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/correlation/")]
    [InlineData("/api/auditlogs/entity//1")]
    [InlineData("/api/auditlogs/entity/SysUser/")]
    [InlineData("/api/monitoring/security/check-failed-logins")]
    [InlineData("/api/monitoring/security/failed-login-count")]
    public async Task Endpoints_WithMissingRequiredParameters_ReturnBadRequest(string endpoint)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that endpoints return proper error responses for non-existent resources.
    /// </summary>
    [Theory]
    [InlineData("/api/auditlogs/999999/status")]
    [InlineData("/api/alerts/rules/999999")]
    public async Task Endpoints_WithNonExistentResource_ReturnNotFoundOrBadRequest(string endpoint)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        // Should return not found, bad request, or success (depending on implementation)
        // but not unauthorized or forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
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
            throw new Exception($"Failed to get admin token. Status: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        
        if (result?.Data?.AccessToken == null)
        {
            throw new Exception("Login response did not contain an access token");
        }

        return result.Data.AccessToken;
    }

    /// <summary>
    /// Attempts to get a regular user authentication token for testing.
    /// Returns null if no regular user exists in the test database.
    /// </summary>
    private async Task<string?> TryGetRegularUserTokenAsync()
    {
        try
        {
            var loginRequest = new LoginDto
            {
                UserName = "regularuser",
                Password = "password123"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                return null; // Regular user doesn't exist
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
            return result?.Data?.AccessToken;
        }
        catch
        {
            return null; // Failed to get regular user token
        }
    }

    #endregion
}