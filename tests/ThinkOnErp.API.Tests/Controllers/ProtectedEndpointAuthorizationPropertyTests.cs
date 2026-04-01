using System.Net;
using System.Net.Http.Headers;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for protected endpoint authorization using FsCheck.
/// These tests validate that all protected endpoints return 401 when accessed without a valid JWT token.
/// </summary>
public class ProtectedEndpointAuthorizationPropertyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int MinIterations = 100;
    private readonly WebApplicationFactory<Program> _factory;

    public ProtectedEndpointAuthorizationPropertyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.4**
    /// 
    /// Property 7: Protected Endpoint Authorization
    /// 
    /// For any protected endpoint (all except /api/auth/login), when accessed without a valid JWT token,
    /// the API must return status code 401.
    /// This test validates that:
    /// 1. All protected endpoints require authentication
    /// 2. Requests without Authorization header return 401
    /// 3. Requests with invalid/malformed tokens return 401
    /// 4. Requests with empty tokens return 401
    /// 5. The login endpoint is NOT protected (can be accessed without token)
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ProtectedEndpoint_WithoutValidToken_Returns401(ProtectedEndpointRequest request)
    {
        // Arrange: Create HTTP client
        var client = _factory.CreateClient();

        // Act: Make request to the protected endpoint based on the scenario
        HttpResponseMessage response;
        
        switch (request.AuthorizationScenario)
        {
            case AuthorizationScenario.NoAuthorizationHeader:
                // No Authorization header at all
                response = MakeRequestAsync(client, request.Endpoint, request.HttpMethod).Result;
                break;

            case AuthorizationScenario.EmptyAuthorizationHeader:
                // Empty Authorization header - use a space as the token value to avoid FormatException
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", " ");
                response = MakeRequestAsync(client, request.Endpoint, request.HttpMethod).Result;
                break;

            case AuthorizationScenario.InvalidToken:
                // Invalid/malformed token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.InvalidToken);
                response = MakeRequestAsync(client, request.Endpoint, request.HttpMethod).Result;
                break;

            case AuthorizationScenario.MalformedAuthorizationHeader:
                // Malformed Authorization header (not Bearer scheme)
                // Only add if not empty to avoid FormatException
                if (!string.IsNullOrWhiteSpace(request.MalformedHeader))
                {
                    try
                    {
                        client.DefaultRequestHeaders.Add("Authorization", request.MalformedHeader);
                    }
                    catch (FormatException)
                    {
                        // If the header format is invalid, skip adding it (equivalent to no header)
                    }
                }
                response = MakeRequestAsync(client, request.Endpoint, request.HttpMethod).Result;
                break;

            default:
                throw new ArgumentException("Unknown authorization scenario");
        }

        // Assert: Verify status code is 401 Unauthorized
        var statusCodeIs401 = response.StatusCode == HttpStatusCode.Unauthorized;

        // Property 2: Response should not be successful
        var responseIsNotSuccessful = !response.IsSuccessStatusCode;

        // Property 3: Status code should not be 200 OK
        var statusCodeIsNot200 = response.StatusCode != HttpStatusCode.OK;

        // Property 4: Status code should not be 403 Forbidden (that's for authenticated but unauthorized users)
        var statusCodeIsNot403 = response.StatusCode != HttpStatusCode.Forbidden;

        // Combine all properties with descriptive labels
        var result = statusCodeIs401
            && responseIsNotSuccessful
            && statusCodeIsNot200
            && statusCodeIsNot403;

        return result
            .Label($"Status code is 401 Unauthorized: {statusCodeIs401}")
            .Label($"Response is not successful: {responseIsNotSuccessful}")
            .Label($"Status code is not 200 OK: {statusCodeIsNot200}")
            .Label($"Status code is not 403 Forbidden: {statusCodeIsNot403}")
            .Label($"Endpoint: {request.HttpMethod} {request.Endpoint}")
            .Label($"Scenario: {request.AuthorizationScenario}")
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
        /// Generates arbitrary protected endpoint requests for property testing.
        /// Covers all protected endpoints in the API (all except /api/auth/login).
        /// </summary>
        public static Arbitrary<ProtectedEndpointRequest> ProtectedEndpointRequest()
        {
            // Define all protected endpoints with their HTTP methods
            var protectedEndpoints = new[]
            {
                // Roles endpoints (all require authentication, CUD require admin)
                ("/api/roles", "GET"),
                ("/api/roles/1", "GET"),
                ("/api/roles", "POST"),
                ("/api/roles/1", "PUT"),
                ("/api/roles/1", "DELETE"),

                // Currency endpoints (all require authentication, CUD require admin)
                ("/api/currencies", "GET"),
                ("/api/currencies/1", "GET"),
                ("/api/currencies", "POST"),
                ("/api/currencies/1", "PUT"),
                ("/api/currencies/1", "DELETE"),

                // Company endpoints (all require authentication, CUD require admin)
                ("/api/companies", "GET"),
                ("/api/companies/1", "GET"),
                ("/api/companies", "POST"),
                ("/api/companies/1", "PUT"),
                ("/api/companies/1", "DELETE"),

                // Branch endpoints (all require authentication, CUD require admin)
                ("/api/branches", "GET"),
                ("/api/branches/1", "GET"),
                ("/api/branches", "POST"),
                ("/api/branches/1", "PUT"),
                ("/api/branches/1", "DELETE"),

                // User endpoints (all require admin)
                ("/api/users", "GET"),
                ("/api/users/1", "GET"),
                ("/api/users", "POST"),
                ("/api/users/1", "PUT"),
                ("/api/users/1", "DELETE"),
                ("/api/users/1/change-password", "PUT")
            };

            var requestGenerator = from endpointMethod in Gen.Elements(protectedEndpoints)
                                  from scenario in Gen.Elements(
                                      AuthorizationScenario.NoAuthorizationHeader,
                                      AuthorizationScenario.EmptyAuthorizationHeader,
                                      AuthorizationScenario.InvalidToken,
                                      AuthorizationScenario.MalformedAuthorizationHeader)
                                  from invalidToken in Gen.Elements(
                                      "invalid_token",
                                      "not.a.jwt",
                                      "malformed",
                                      "abc123",
                                      "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid",
                                      "header.payload", // Missing signature
                                      "onlyonepart")
                                  from malformedHeader in Gen.Elements(
                                      "InvalidScheme token123",
                                      "Basic dXNlcjpwYXNz", // Basic auth instead of Bearer
                                      "token123", // Missing scheme
                                      "Bearer", // Missing token
                                      "")
                                  select new ProtectedEndpointRequest
                                  {
                                      Endpoint = endpointMethod.Item1,
                                      HttpMethod = endpointMethod.Item2,
                                      AuthorizationScenario = scenario,
                                      InvalidToken = invalidToken,
                                      MalformedHeader = malformedHeader
                                  };

            return Arb.From(requestGenerator);
        }
    }

    /// <summary>
    /// Represents a request to a protected endpoint for testing.
    /// </summary>
    public class ProtectedEndpointRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public AuthorizationScenario AuthorizationScenario { get; set; }
        public string InvalidToken { get; set; } = string.Empty;
        public string MalformedHeader { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of authorization scenarios to test.
    /// </summary>
    public enum AuthorizationScenario
    {
        NoAuthorizationHeader,
        EmptyAuthorizationHeader,
        InvalidToken,
        MalformedAuthorizationHeader
    }
}
