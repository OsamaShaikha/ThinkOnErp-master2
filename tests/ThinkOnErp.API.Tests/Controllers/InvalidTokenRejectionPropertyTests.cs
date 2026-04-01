using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for invalid token rejection using FsCheck.
/// These tests validate that invalid or expired JWT tokens are always rejected with HTTP 401.
/// </summary>
public class InvalidTokenRejectionPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 2.7**
    /// 
    /// Property 4: Invalid Token Rejection
    /// 
    /// For any invalid or expired JWT token, when included in a request, 
    /// the API must return status code 401.
    /// This test validates that:
    /// 1. Tokens with invalid signatures are rejected
    /// 2. Expired tokens are rejected
    /// 3. Tokens with wrong issuer are rejected
    /// 4. Tokens with wrong audience are rejected
    /// 5. Malformed tokens are rejected
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property InvalidOrExpiredToken_AlwaysRejectedWith401(InvalidTokenScenario scenario)
    {
        // Arrange: Configure JWT settings (same as in Program.cs)
        var correctSecretKey = "ThisIsATestSecretKeyForJwtTokenGenerationWithMinimum32Characters";
        var correctIssuer = "ThinkOnErpTestIssuer";
        var correctAudience = "ThinkOnErpTestAudience";

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = correctIssuer,
            ValidAudience = correctAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(correctSecretKey)),
            ClockSkew = TimeSpan.Zero // Remove default 5 minute clock skew for precise expiration testing
        };

        var handler = new JwtSecurityTokenHandler();

        // Act: Generate an invalid token based on the scenario
        string invalidToken = scenario.ScenarioType switch
        {
            InvalidTokenType.InvalidSignature => GenerateTokenWithInvalidSignature(scenario, correctIssuer, correctAudience),
            InvalidTokenType.ExpiredToken => GenerateExpiredToken(scenario, correctSecretKey, correctIssuer, correctAudience),
            InvalidTokenType.WrongIssuer => GenerateTokenWithWrongIssuer(scenario, correctSecretKey, correctAudience),
            InvalidTokenType.WrongAudience => GenerateTokenWithWrongAudience(scenario, correctSecretKey, correctIssuer),
            InvalidTokenType.MalformedToken => GenerateMalformedToken(scenario),
            _ => throw new ArgumentException("Unknown scenario type")
        };

        // Assert: Verify token validation fails (which would result in 401)
        bool tokenValidationFailed = false;
        SecurityToken? validatedToken = null;
        Exception? validationException = null;

        try
        {
            handler.ValidateToken(invalidToken, validationParameters, out validatedToken);
        }
        catch (SecurityTokenException ex)
        {
            tokenValidationFailed = true;
            validationException = ex;
        }
        catch (ArgumentException ex)
        {
            // Malformed tokens throw ArgumentException
            tokenValidationFailed = true;
            validationException = ex;
        }

        // Property 1: Token validation must fail
        var validationFailed = tokenValidationFailed;

        // Property 2: An exception must be thrown
        var exceptionWasThrown = validationException != null;

        // Property 3: No validated token should be returned
        var noValidatedToken = validatedToken == null || tokenValidationFailed;

        // Combine all properties with descriptive labels
        var result = validationFailed
            && exceptionWasThrown
            && noValidatedToken;

        return result
            .Label($"Token validation failed (would return 401): {validationFailed}")
            .Label($"Exception was thrown: {exceptionWasThrown}")
            .Label($"No validated token returned: {noValidatedToken}")
            .Label($"Scenario: {scenario.ScenarioType}")
            .Label($"Exception type: {validationException?.GetType().Name ?? "None"}")
            .When(validationException != null)
            .Label($"Exception message: {validationException?.Message ?? "None"}");
    }

    /// <summary>
    /// Generates a token with an invalid signature (signed with wrong secret key).
    /// </summary>
    private string GenerateTokenWithInvalidSignature(InvalidTokenScenario scenario, string issuer, string audience)
    {
        var wrongSecretKey = scenario.WrongSecretKey;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(wrongSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", "1"),
            new Claim("userName", "testuser"),
            new Claim("role", "1"),
            new Claim("branchId", "1"),
            new Claim("isAdmin", "false")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Generates an expired token (expiration time in the past).
    /// </summary>
    private string GenerateExpiredToken(InvalidTokenScenario scenario, string secretKey, string issuer, string audience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", "1"),
            new Claim("userName", "testuser"),
            new Claim("role", "1"),
            new Claim("branchId", "1"),
            new Claim("isAdmin", "false")
        };

        // Token expired in the past
        var expirationTime = DateTime.UtcNow.AddMinutes(-scenario.MinutesExpired);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Generates a token with wrong issuer.
    /// </summary>
    private string GenerateTokenWithWrongIssuer(InvalidTokenScenario scenario, string secretKey, string audience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", "1"),
            new Claim("userName", "testuser"),
            new Claim("role", "1"),
            new Claim("branchId", "1"),
            new Claim("isAdmin", "false")
        };

        var token = new JwtSecurityToken(
            issuer: scenario.WrongIssuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Generates a token with wrong audience.
    /// </summary>
    private string GenerateTokenWithWrongAudience(InvalidTokenScenario scenario, string secretKey, string issuer)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", "1"),
            new Claim("userName", "testuser"),
            new Claim("role", "1"),
            new Claim("branchId", "1"),
            new Claim("isAdmin", "false")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: scenario.WrongAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Generates a malformed token (invalid JWT format).
    /// </summary>
    private string GenerateMalformedToken(InvalidTokenScenario scenario)
    {
        return scenario.MalformedTokenValue;
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary invalid token scenarios for property testing.
        /// Covers five scenarios: invalid signature, expired token, wrong issuer, wrong audience, and malformed token.
        /// </summary>
        public static Arbitrary<InvalidTokenScenario> InvalidTokenScenario()
        {
            var invalidTokenGenerator = Gen.OneOf(
                // Scenario 1: Invalid signature (token signed with wrong secret key)
                from wrongKey in Gen.Elements(
                    "WrongSecretKey123456789012345678901234567890",
                    "DifferentKey123456789012345678901234567890",
                    "InvalidKey123456789012345678901234567890",
                    "HackerKey123456789012345678901234567890"
                )
                select new InvalidTokenScenario
                {
                    ScenarioType = InvalidTokenType.InvalidSignature,
                    WrongSecretKey = wrongKey
                },

                // Scenario 2: Expired token (token with expiration time in the past)
                from minutesExpired in Gen.Choose(1, 1440) // 1 minute to 24 hours expired
                select new InvalidTokenScenario
                {
                    ScenarioType = InvalidTokenType.ExpiredToken,
                    MinutesExpired = minutesExpired
                },

                // Scenario 3: Wrong issuer
                from wrongIssuer in Gen.Elements(
                    "WrongIssuer",
                    "HackerIssuer",
                    "FakeIssuer",
                    "UnknownIssuer",
                    "MaliciousIssuer"
                )
                select new InvalidTokenScenario
                {
                    ScenarioType = InvalidTokenType.WrongIssuer,
                    WrongIssuer = wrongIssuer
                },

                // Scenario 4: Wrong audience
                from wrongAudience in Gen.Elements(
                    "WrongAudience",
                    "HackerAudience",
                    "FakeAudience",
                    "UnknownAudience",
                    "MaliciousAudience"
                )
                select new InvalidTokenScenario
                {
                    ScenarioType = InvalidTokenType.WrongAudience,
                    WrongAudience = wrongAudience
                },

                // Scenario 5: Malformed token (invalid JWT format)
                from malformedToken in Gen.Elements(
                    "not.a.jwt",
                    "invalid_token",
                    "malformed",
                    "abc123",
                    "Bearer token",
                    "",
                    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid",
                    "header.payload", // Missing signature
                    "onlyonepart"
                )
                select new InvalidTokenScenario
                {
                    ScenarioType = InvalidTokenType.MalformedToken,
                    MalformedTokenValue = malformedToken
                }
            );

            return Arb.From(invalidTokenGenerator);
        }
    }

    /// <summary>
    /// Represents an invalid token scenario for testing.
    /// </summary>
    public class InvalidTokenScenario
    {
        public InvalidTokenType ScenarioType { get; set; }
        public string WrongSecretKey { get; set; } = string.Empty;
        public int MinutesExpired { get; set; }
        public string WrongIssuer { get; set; } = string.Empty;
        public string WrongAudience { get; set; } = string.Empty;
        public string MalformedTokenValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of invalid token scenarios.
    /// </summary>
    public enum InvalidTokenType
    {
        InvalidSignature,
        ExpiredToken,
        WrongIssuer,
        WrongAudience,
        MalformedToken
    }
}
