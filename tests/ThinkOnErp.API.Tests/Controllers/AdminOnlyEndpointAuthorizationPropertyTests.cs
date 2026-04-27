using System.Net;
using System.Net.Http.Headers;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for admin-only endpoint authorization using FsCheck.
/// These tests validate that admin-only endpoints return 403 when accessed by non-admin users.
/// </summary>
public class AdminOnlyEndpointAuthorizationPropertyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int MinIterations = 100;
    private readonly WebApplicationFactory<Program> _factory;

    public AdminOnlyEndpointAuthorizationPropertyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// **Validates: Requirements 4.3, 4.5**
    /// 
    /// Property 8: Admin-Only Endpoint Authorization
    /// 
    /// For any admin-only endpoint accessed by non-admin user, verify status code 403.
    /// This test validates that:
    /// 1. Non-admin users (isAdmin claim is false) cannot access admin-only endpoints
    /// 2. Valid JWT tokens with isAdmin=false are authenticated but not authorized
    /// 3. The API returns 403 Forbidden (not 401 Unauthorized)
    /// 4. All admin-only endpoints enforce the AdminOnly policy
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property AdminOnlyEndpoint_WithNonAdminUser_Returns403(AdminOnlyEndpointRequest request)
    {
        // Arrange: Create HTTP client and generate valid JWT token for non-admin user
        var client = _factory.CreateClient();

        // Configure JWT settings (must match appsettings.json configuration)
        var secretKey = "your-secret-key-here-must-be-at-least-32-characters-long-for-security";
        var issuer = "ThinkOnErpAPI";
        var audience = "ThinkOnErpClient";
        var expiryInMinutes = 60;

        var configData = new Dictionary<string, string>
        {
            { "JwtSettings:SecretKey", secretKey },
            { "JwtSettings:Issuer", issuer },
            { "JwtSettings:Audience", audience },
            { "JwtSettings:ExpiryInMinutes", expiryInMinutes.ToString() }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        var jwtTokenService = new JwtTokenService(configuration);

        // Generate token for non-admin user (isAdmin = false)
        var nonAdminUser = new SysUser
        {
            RowId = request.UserId,
            UserName = request.UserName,
            RowDesc = "Non-Admin User",
            RowDescE = "Non-Admin User",
            Password = "hash",
            Role = request.RoleId,
            BranchId = request.BranchId,
            IsAdmin = false, // Critical: This must be false
            IsActive = true,
            CreationUser = "system",
            CreationDate = DateTime.UtcNow
        };

        var tokenDto = jwtTokenService.GenerateToken(nonAdminUser);

        // Add the valid JWT token to the request
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDto.AccessToken);

        // Act: Make request to the admin-only endpoint
        HttpResponseMessage response = MakeRequestAsync(client, request.Endpoint, request.HttpMethod).Result;

        // Assert: Verify status code is 403 Forbidden
        var statusCodeIs403 = response.StatusCode == HttpStatusCode.Forbidden;

        // Property 2: Response should not be successful
        var responseIsNotSuccessful = !response.IsSuccessStatusCode;

        // Property 3: Status code should not be 401 Unauthorized (user is authenticated, just not authorized)
        var statusCodeIsNot401 = response.StatusCode != HttpStatusCode.Unauthorized;

        // Property 4: Status code should not be 200 OK
        var statusCodeIsNot200 = response.StatusCode != HttpStatusCode.OK;

        // Property 5: Status code should not be 201 Created
        var statusCodeIsNot201 = response.StatusCode != HttpStatusCode.Created;

        // Combine all properties with descriptive labels
        var result = statusCodeIs403
            && responseIsNotSuccessful
            && statusCodeIsNot401
            && statusCodeIsNot200
            && statusCodeIsNot201;

        return result
            .Label($"Status code is 403 Forbidden: {statusCodeIs403}")
            .Label($"Response is not successful: {responseIsNotSuccessful}")
            .Label($"Status code is not 401 Unauthorized: {statusCodeIsNot401}")
            .Label($"Status code is not 200 OK: {statusCodeIsNot200}")
            .Label($"Status code is not 201 Created: {statusCodeIsNot201}")
            .Label($"Endpoint: {request.HttpMethod} {request.Endpoint}")
            .Label($"User: {request.UserName} (IsAdmin: false)")
            .Label($"Actual status code: {(int)response.StatusCode} ({response.StatusCode})");
    }

    /// <summary>
    /// Helper method to make HTTP requests with different methods.
    /// </summary>
    private async Task<HttpResponseMessage> MakeRequestAsync(HttpClient client, string endpoint, string httpMethod)
    {
        return httpMethod.ToUpper() switch
        {
            "GET" => await client.GetAsync(endpoint),
            "POST" => await client.PostAsync(endpoint, new StringContent("{}", System.Text.Encoding.UTF8, "application/json")),
            "PUT" => await client.PutAsync(endpoint, new StringContent("{}", System.Text.Encoding.UTF8, "application/json")),
            "DELETE" => await client.DeleteAsync(endpoint),
            _ => throw new ArgumentException($"Unsupported HTTP method: {httpMethod}")
        };
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary admin-only endpoint requests for property testing.
        /// Covers all admin-only endpoints in the API.
        /// </summary>
        public static Arbitrary<AdminOnlyEndpointRequest> AdminOnlyEndpointRequest()
        {
            // Define all admin-only endpoints with their HTTP methods
            var adminOnlyEndpoints = new[]
            {
                // Roles endpoints (CUD operations require admin)
                ("/api/roles", "POST"),
                ("/api/roles/1", "PUT"),
                ("/api/roles/1", "DELETE"),

                // Currency endpoints (CUD operations require admin)
                ("/api/currencies", "POST"),
                ("/api/currencies/1", "PUT"),
                ("/api/currencies/1", "DELETE"),

                // Company endpoints (CUD operations require admin)
                ("/api/companies", "POST"),
                ("/api/companies/1", "PUT"),
                ("/api/companies/1", "DELETE"),

                // Branch endpoints (CUD operations require admin)
                ("/api/branches", "POST"),
                ("/api/branches/1", "PUT"),
                ("/api/branches/1", "DELETE"),

                // User endpoints (all operations require admin)
                ("/api/users", "GET"),
                ("/api/users/1", "GET"),
                ("/api/users", "POST"),
                ("/api/users/1", "PUT"),
                ("/api/users/1", "DELETE"),

                // Audit logs endpoints (all operations require admin)
                ("/api/auditlogs/legacy", "GET"),
                ("/api/auditlogs/dashboard", "GET"),
                ("/api/auditlogs/legacy/1/status", "PUT"),
                ("/api/auditlogs/1/status", "GET"),
                ("/api/auditlogs/transform", "POST")
            };

            var requestGenerator = from endpointMethod in Gen.Elements(adminOnlyEndpoints)
                                  from userId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                                  from userName in Gen.Elements("user1", "testuser", "john.doe", "jane.smith", "employee")
                                  from roleId in Gen.Choose(1, 100).Select(i => (Int64?)i)
                                  from branchId in Gen.Choose(1, 100).Select(i => (Int64?)i)
                                  select new AdminOnlyEndpointRequest
                                  {
                                      Endpoint = endpointMethod.Item1,
                                      HttpMethod = endpointMethod.Item2,
                                      UserId = userId,
                                      UserName = userName,
                                      RoleId = roleId,
                                      BranchId = branchId
                                  };

            return Arb.From(requestGenerator);
        }
    }

    /// <summary>
    /// Represents a request to an admin-only endpoint for testing.
    /// </summary>
    public class AdminOnlyEndpointRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public Int64 UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public Int64? RoleId { get; set; }
        public Int64? BranchId { get; set; }
    }
}
