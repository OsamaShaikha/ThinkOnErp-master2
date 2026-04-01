using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for valid token authentication using FsCheck.
/// These tests validate that valid JWT tokens are always accepted by the authentication system.
/// </summary>
public class ValidTokenAuthenticationPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 2.6**
    /// 
    /// Property 3: Valid Token Authentication
    /// 
    /// For any valid JWT token in request to protected endpoint, verify authentication succeeds.
    /// This test validates that:
    /// 1. A valid JWT token can be successfully validated by the authentication system
    /// 2. The token signature is valid
    /// 3. The token is not expired
    /// 4. The token contains all required claims
    /// 5. The issuer and audience match the configuration
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ValidToken_CanBeValidatedSuccessfully(SysUser user)
    {
        // Arrange: Configure JWT settings
        var secretKey = "ThisIsATestSecretKeyForJwtTokenGenerationWithMinimum32Characters";
        var issuer = "ThinkOnErpTestIssuer";
        var audience = "ThinkOnErpTestAudience";
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

        // Act: Generate a valid JWT token
        var tokenDto = jwtTokenService.GenerateToken(user);

        // Parse the token
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenDto.AccessToken);

        // Setup validation parameters (same as in Program.cs)
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Assert: Verify authentication succeeds
        // Property 1: Token can be validated successfully (no exception thrown)
        bool tokenIsValid;
        ClaimsPrincipal? principal = null;
        try
        {
            principal = handler.ValidateToken(tokenDto.AccessToken, validationParameters, out _);
            tokenIsValid = true;
        }
        catch
        {
            tokenIsValid = false;
        }

        // Property 2: Token is not expired
        var tokenIsNotExpired = token.ValidTo > DateTime.UtcNow;

        // Property 3: Token has valid signature (verified by ValidateToken)
        var hasValidSignature = tokenIsValid;

        // Property 4: Token contains all required claims
        var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == "userId");
        var userNameClaim = token.Claims.FirstOrDefault(c => c.Type == "userName");
        var roleClaim = token.Claims.FirstOrDefault(c => c.Type == "role");
        var branchIdClaim = token.Claims.FirstOrDefault(c => c.Type == "branchId");
        var isAdminClaim = token.Claims.FirstOrDefault(c => c.Type == "isAdmin");

        var hasAllRequiredClaims = userIdClaim != null
            && userNameClaim != null
            && roleClaim != null
            && branchIdClaim != null
            && isAdminClaim != null;

        // Property 5: Token has correct issuer and audience
        var hasCorrectIssuerAndAudience = token.Issuer == issuer
            && token.Audiences.Contains(audience);

        // Property 6: Principal is not null when token is valid
        var principalIsNotNull = tokenIsValid ? principal != null : true;

        // Property 7: Principal contains the expected claims when token is valid
        var principalHasExpectedClaims = true;
        if (tokenIsValid && principal != null)
        {
            var principalUserId = principal.FindFirst("userId")?.Value;
            var principalUserName = principal.FindFirst("userName")?.Value;
            var principalIsAdmin = principal.FindFirst("isAdmin")?.Value;

            principalHasExpectedClaims = principalUserId == user.RowId.ToString()
                && principalUserName == user.UserName
                && principalIsAdmin == user.IsAdmin.ToString().ToLower();
        }

        // Property 8: Token type is Bearer
        var tokenTypeIsCorrect = tokenDto.TokenType == "Bearer";

        // Combine all properties with descriptive labels
        var result = tokenIsValid
            && tokenIsNotExpired
            && hasValidSignature
            && hasAllRequiredClaims
            && hasCorrectIssuerAndAudience
            && principalIsNotNull
            && principalHasExpectedClaims
            && tokenTypeIsCorrect;

        return result
            .Label($"Token is valid (authentication succeeds): {tokenIsValid}")
            .Label($"Token is not expired: {tokenIsNotExpired}")
            .Label($"Has valid signature: {hasValidSignature}")
            .Label($"Has all required claims: {hasAllRequiredClaims}")
            .Label($"Has correct issuer and audience: {hasCorrectIssuerAndAudience}")
            .Label($"Principal is not null: {principalIsNotNull}")
            .Label($"Principal has expected claims: {principalHasExpectedClaims}")
            .Label($"Token type is correct: {tokenTypeIsCorrect}")
            .Label($"User: {user.UserName}, IsAdmin: {user.IsAdmin}");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary valid SysUser instances for property testing.
        /// </summary>
        public static Arbitrary<SysUser> SysUser()
        {
            var userGenerator = from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
                               from userName in Gen.Elements("user1", "admin", "testuser", "john.doe", "jane.smith")
                               from rowDesc in Gen.Elements("مستخدم", "مدير", "موظف")
                               from rowDescE in Gen.Elements("User", "Admin", "Employee")
                               from password in Gen.Elements("hash1", "hash2", "hash3")
                               from role in Gen.Choose(1, 100).Select(i => (decimal?)i)
                               from branchId in Gen.Choose(1, 100).Select(i => (decimal?)i)
                               from isAdmin in Arb.Generate<bool>()
                               from creationUser in Gen.Elements("admin", "system", "root")
                               select new Domain.Entities.SysUser
                               {
                                   RowId = rowId,
                                   UserName = userName,
                                   RowDesc = rowDesc,
                                   RowDescE = rowDescE,
                                   Password = password,
                                   Role = role,
                                   BranchId = branchId,
                                   IsAdmin = isAdmin,
                                   IsActive = true,
                                   CreationUser = creationUser,
                                   CreationDate = DateTime.UtcNow
                               };

            return Arb.From(userGenerator);
        }
    }
}
