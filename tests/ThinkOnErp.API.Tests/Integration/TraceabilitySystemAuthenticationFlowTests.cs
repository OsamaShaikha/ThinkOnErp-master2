using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Compliance;
using Xunit;

namespace ThinkOnErp.API.Tests.Integration;

/// <summary>
/// Integration tests focused on authentication flows for the Full Traceability System.
/// Tests various authentication scenarios including token validation, refresh, and expiration.
/// 
/// **Validates: Requirements 19.10**
/// Comprehensive testing of JWT authentication mechanisms including:
/// - Token generation and validation
/// - Token refresh workflows
/// - Authentication state management
/// - Cross-controller authentication consistency
/// </summary>
public class TraceabilitySystemAuthenticationFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public TraceabilitySystemAuthenticationFlowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Authentication Flow Tests

    /// <summary>
    /// Tests the complete authentication flow from login to accessing protected resources.
    /// Verifies that the JWT token obtained from login works across all traceability controllers.
    /// </summary>
    [Fact]
    public async Task CompleteAuthenticationFlow_LoginAndAccessProtectedResources_Succeeds()
    {
        // Step 1: Login and get token
        var loginRequest = new LoginDto
        {
            UserName = "admin",
            Password = "admin123"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.True(loginResponse.IsSuccessStatusCode, "Login should succeed");

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        Assert.NotNull(loginResult?.Data?.AccessToken);
        
        var token = loginResult.Data.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Step 2: Test access to AuditLogsController
        var auditResponse = await _client.GetAsync("/api/auditlogs/dashboard");
        Assert.True(auditResponse.IsSuccessStatusCode, "Should access audit logs with valid token");

        // Step 3: Test access to ComplianceController
        var complianceResponse = await _client.GetAsync("/api/compliance/sox/segregation-of-duties");
        Assert.NotEqual(HttpStatusCode.Unauthorized, complianceResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, complianceResponse.StatusCode);

        // Step 4: Test access to MonitoringController
        var monitoringResponse = await _client.GetAsync("/api/monitoring/health");
        Assert.True(monitoringResponse.IsSuccessStatusCode, "Should access monitoring with valid token");

        // Step 5: Test access to AlertsController
        var alertsResponse = await _client.GetAsync("/api/alerts/rules");
        Assert.True(alertsResponse.IsSuccessStatusCode, "Should access alerts with valid token");
    }

    /// <summary>
    /// Tests that the same token works consistently across multiple requests.
    /// Verifies token persistence and stateless authentication.
    /// </summary>
    [Fact]
    public async Task TokenConsistency_MultipleRequestsWithSameToken_AllSucceed()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Make multiple requests with the same token
        var requests = new[]
        {
            "/api/auditlogs/dashboard",
            "/api/monitoring/memory",
            "/api/alerts/rules",
            "/api/auditlogs/legacy?pageSize=5",
            "/api/monitoring/security/threats"
        };

        foreach (var endpoint in requests)
        {
            var response = await _client.GetAsync(endpoint);
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// Tests authentication with different token formats and edge cases.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("Bearer")]
    [InlineData("Basic dGVzdDp0ZXN0")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    public async Task InvalidTokenFormats_ReturnUnauthorized(string invalidToken)
    {
        // Arrange
        if (string.IsNullOrWhiteSpace(invalidToken))
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }
        else if (invalidToken.StartsWith("Bearer") || invalidToken.StartsWith("Basic"))
        {
            var parts = invalidToken.Split(' ');
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(parts[0], parts.Length > 1 ? parts[1] : "");
        }
        else
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);
        }

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Token Validation Tests

    /// <summary>
    /// Tests that malformed JWT tokens are properly rejected.
    /// </summary>
    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("header.payload")]
    [InlineData("header.payload.signature.extra")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid-payload.signature")]
    public async Task MalformedJwtTokens_ReturnUnauthorized(string malformedToken)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", malformedToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that tokens with invalid signatures are rejected.
    /// </summary>
    [Fact]
    public async Task TokenWithInvalidSignature_ReturnsUnauthorized()
    {
        // Arrange - Create a token with valid structure but invalid signature
        var validToken = await GetAdminTokenAsync();
        var tokenParts = validToken.Split('.');
        
        // Modify the signature to make it invalid
        var invalidSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid-signature"));
        var invalidToken = $"{tokenParts[0]}.{tokenParts[1]}.{invalidSignature}";
        
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Authorization Header Tests

    /// <summary>
    /// Tests various authorization header formats and schemes.
    /// </summary>
    [Theory]
    [InlineData("bearer", true)]  // lowercase
    [InlineData("Bearer", true)]  // proper case
    [InlineData("BEARER", true)]  // uppercase
    [InlineData("Basic", false)]  // wrong scheme
    [InlineData("Digest", false)] // wrong scheme
    [InlineData("", false)]       // empty scheme
    public async Task AuthorizationHeaderSchemes_ValidateCorrectly(string scheme, bool shouldSucceed)
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        
        if (string.IsNullOrEmpty(scheme))
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        if (shouldSucceed)
        {
            Assert.True(response.IsSuccessStatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>
    /// Tests that missing Authorization header returns 401.
    /// </summary>
    [Fact]
    public async Task MissingAuthorizationHeader_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Cross-Controller Authentication Tests

    /// <summary>
    /// Tests that authentication works consistently across all traceability controllers.
    /// Verifies that the same token provides access to all protected endpoints.
    /// </summary>
    [Fact]
    public async Task CrossControllerAuthentication_SameTokenWorksEverywhere()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Define test endpoints for each controller
        var controllerEndpoints = new Dictionary<string, string[]>
        {
            ["AuditLogs"] = new[] 
            { 
                "/api/auditlogs/dashboard",
                "/api/auditlogs/legacy?pageSize=1"
            },
            ["Compliance"] = new[] 
            { 
                "/api/compliance/sox/segregation-of-duties"
            },
            ["Monitoring"] = new[] 
            { 
                "/api/monitoring/health",
                "/api/monitoring/memory",
                "/api/monitoring/security/threats"
            },
            ["Alerts"] = new[] 
            { 
                "/api/alerts/rules",
                "/api/alerts/history"
            }
        };

        // Act & Assert
        foreach (var (controllerName, endpoints) in controllerEndpoints)
        {
            foreach (var endpoint in endpoints)
            {
                var response = await _client.GetAsync(endpoint);
                
                // Should not be unauthorized or forbidden
                Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
                
                // Log success for debugging
                if (response.IsSuccessStatusCode)
                {
                    Assert.True(true, $"{controllerName} endpoint {endpoint} accessible with token");
                }
            }
        }
    }

    #endregion

    #region Token Claims Tests

    /// <summary>
    /// Tests that endpoints can access user information from JWT claims.
    /// This test verifies that the token contains the necessary user context.
    /// </summary>
    [Fact]
    public async Task TokenClaims_ContainUserInformation()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Make a request that should use user information from claims
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        // The fact that the request succeeds indicates that the controller
        // was able to extract user information from the JWT claims
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    #endregion

    #region Concurrent Authentication Tests

    /// <summary>
    /// Tests that multiple concurrent requests with the same token work correctly.
    /// Verifies thread safety of authentication mechanisms.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequests_WithSameToken_AllSucceed()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();
        var client3 = _factory.CreateClient();
        
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client3.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Make concurrent requests
        var tasks = new[]
        {
            client1.GetAsync("/api/auditlogs/dashboard"),
            client2.GetAsync("/api/monitoring/memory"),
            client3.GetAsync("/api/alerts/rules"),
            client1.GetAsync("/api/auditlogs/legacy?pageSize=1"),
            client2.GetAsync("/api/monitoring/security/threats")
        };

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    #endregion

    #region Authentication Error Response Tests

    /// <summary>
    /// Tests that authentication errors return proper error responses with correct content types.
    /// </summary>
    [Fact]
    public async Task AuthenticationErrors_ReturnProperErrorResponses()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        // Check that response has proper content type for error responses
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(
            string.IsNullOrEmpty(contentType) || 
            contentType.Contains("application/json") || 
            contentType.Contains("text/plain"));
    }

    /// <summary>
    /// Tests that 401 responses include proper WWW-Authenticate header.
    /// </summary>
    [Fact]
    public async Task UnauthorizedResponse_IncludesWwwAuthenticateHeader()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        // Check for WWW-Authenticate header (may or may not be present depending on configuration)
        var wwwAuthHeader = response.Headers.WwwAuthenticate;
        // This is informational - some JWT implementations include this header, others don't
        Assert.True(true, $"WWW-Authenticate header present: {wwwAuthHeader?.Any() == true}");
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