using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for JwtTokenService using FsCheck.
/// These tests validate universal properties that should hold for all valid inputs.
/// </summary>
public class JwtTokenServicePropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 2.1, 2.3, 2.4, 2.5**
    /// 
    /// Property 1: JWT Token Structure Completeness
    /// 
    /// For any valid user credentials, when a JWT token is generated, the token must contain 
    /// all required claims (userId, userName, role, branchId, isAdmin), be signed with the 
    /// configured secret key, include the configured issuer and audience, and have an 
    /// expiration time matching the configured ExpiryInMinutes.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property JwtToken_ContainsAllRequiredClaimsAndStructure(SysUser user)
    {
        // Configure test JWT settings
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

        var service = new JwtTokenService(configuration);

        // Capture time before token generation for expiry validation
        var beforeGeneration = DateTime.UtcNow;

        // Act: Generate token
        var tokenDto = service.GenerateToken(user);

        // Capture time after token generation
        var afterGeneration = DateTime.UtcNow;

            // Parse the token
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenDto.AccessToken);

            // Verify all required claims are present
            var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == "userId");
            var userNameClaim = token.Claims.FirstOrDefault(c => c.Type == "userName");
            var roleClaim = token.Claims.FirstOrDefault(c => c.Type == "role");
            var branchIdClaim = token.Claims.FirstOrDefault(c => c.Type == "branchId");
            var isAdminClaim = token.Claims.FirstOrDefault(c => c.Type == "isAdmin");

            // Property 1: All required claims must exist
            var allClaimsExist = userIdClaim != null
                && userNameClaim != null
                && roleClaim != null
                && branchIdClaim != null
                && isAdminClaim != null;

            // Property 2: Claims must have correct values
            var claimsHaveCorrectValues = userIdClaim?.Value == user.RowId.ToString()
                && userNameClaim?.Value == user.UserName
                && roleClaim?.Value == (user.Role?.ToString() ?? "0")
                && branchIdClaim?.Value == (user.BranchId?.ToString() ?? "0")
                && isAdminClaim?.Value == user.IsAdmin.ToString().ToLower();

            // Property 3: Token must be signed with configured secret using HMAC-SHA256
            var isSignedCorrectly = token.Header.Alg == SecurityAlgorithms.HmacSha256;

            // Verify signature is valid
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            bool signatureIsValid;
            try
            {
                handler.ValidateToken(tokenDto.AccessToken, validationParameters, out _);
                signatureIsValid = true;
            }
            catch
            {
                signatureIsValid = false;
            }

            // Property 4: Token must include configured issuer and audience
            var hasCorrectIssuerAndAudience = token.Issuer == issuer
                && token.Audiences.Contains(audience);

            // Property 5: Expiration time must match configured ExpiryInMinutes
            // Allow for 2 seconds tolerance to account for test execution time
            var expectedMinExpiry = beforeGeneration.AddMinutes(expiryInMinutes).AddSeconds(-2);
            var expectedMaxExpiry = afterGeneration.AddMinutes(expiryInMinutes).AddSeconds(2);
            var expirationIsCorrect = token.ValidTo >= expectedMinExpiry && token.ValidTo <= expectedMaxExpiry;

            // Property 6: TokenDto.ExpiresAt must match token.ValidTo
            var expiresAtMatches = Math.Abs((token.ValidTo - tokenDto.ExpiresAt).TotalSeconds) < 1;

            // Property 7: TokenType must be "Bearer"
            var tokenTypeIsCorrect = tokenDto.TokenType == "Bearer";

            // Combine all properties with descriptive labels
            var result = allClaimsExist
                && claimsHaveCorrectValues
                && isSignedCorrectly
                && signatureIsValid
                && hasCorrectIssuerAndAudience
                && expirationIsCorrect
                && expiresAtMatches
                && tokenTypeIsCorrect;

            return result
                .Label($"All claims exist: {allClaimsExist}")
                .Label($"Claims have correct values: {claimsHaveCorrectValues}")
                .Label($"Is signed correctly (HS256): {isSignedCorrectly}")
                .Label($"Signature is valid: {signatureIsValid}")
                .Label($"Has correct issuer and audience: {hasCorrectIssuerAndAudience}")
                .Label($"Expiration is correct: {expirationIsCorrect}")
                .Label($"ExpiresAt matches: {expiresAtMatches}")
                .Label($"Token type is correct: {tokenTypeIsCorrect}");
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
                               from role in Gen.Choose(0, 100).Select(i => (decimal?)i)
                               from branchId in Gen.Choose(0, 100).Select(i => (decimal?)i)
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
