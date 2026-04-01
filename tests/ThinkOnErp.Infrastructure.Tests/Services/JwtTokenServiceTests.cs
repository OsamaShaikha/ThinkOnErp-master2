using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        // Setup configuration with test JWT settings
        var configData = new Dictionary<string, string>
        {
            { "JwtSettings:SecretKey", "ThisIsATestSecretKeyForJwtTokenGenerationWithMinimum32Characters" },
            { "JwtSettings:Issuer", "ThinkOnErpTestIssuer" },
            { "JwtSettings:Audience", "ThinkOnErpTestAudience" },
            { "JwtSettings:ExpiryInMinutes", "60" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _service = new JwtTokenService(_configuration);
    }

    [Fact]
    public void GenerateToken_WithValidUser_ReturnsTokenDto()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_TokenContainsAllRequiredClaims()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        Assert.Contains(token.Claims, c => c.Type == "userId" && c.Value == user.RowId.ToString());
        Assert.Contains(token.Claims, c => c.Type == "userName" && c.Value == user.UserName);
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == user.Role.ToString());
        Assert.Contains(token.Claims, c => c.Type == "branchId" && c.Value == user.BranchId.ToString());
        Assert.Contains(token.Claims, c => c.Type == "isAdmin" && c.Value == user.IsAdmin.ToString().ToLower());
    }

    [Fact]
    public void GenerateToken_TokenHasCorrectIssuerAndAudience()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal("ThinkOnErpTestIssuer", token.Issuer);
        Assert.Contains("ThinkOnErpTestAudience", token.Audiences);
    }

    [Fact]
    public void GenerateToken_TokenExpiresAtCorrectTime()
    {
        // Arrange
        var user = CreateTestUser();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = _service.GenerateToken(user);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        // Token should expire 60 minutes from now (as configured)
        // Allow for small timing differences (up to 1 second)
        var expectedExpiry = beforeGeneration.AddMinutes(60).AddSeconds(-1);
        var maxExpiry = afterGeneration.AddMinutes(60).AddSeconds(1);

        Assert.True(token.ValidTo >= expectedExpiry, $"Token expiry {token.ValidTo} should be >= {expectedExpiry}");
        Assert.True(token.ValidTo <= maxExpiry, $"Token expiry {token.ValidTo} should be <= {maxExpiry}");
        
        // Verify ExpiresAt matches token ValidTo (within 1 second tolerance)
        var timeDifference = Math.Abs((token.ValidTo - result.ExpiresAt).TotalSeconds);
        Assert.True(timeDifference < 1, $"ExpiresAt should match token ValidTo within 1 second, difference: {timeDifference}s");
    }

    [Fact]
    public void GenerateToken_TokenIsSignedWithHmacSha256()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal("HS256", token.Header.Alg);
    }

    [Fact]
    public void GenerateToken_WithNullUser_ThrowsArgumentNullException()
    {
        // Arrange
        SysUser? user = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => _service.GenerateToken(user!));
        Assert.Equal("user", exception.ParamName);
    }

    [Fact]
    public void GenerateToken_WithUserHavingNullRole_UsesZeroForRoleClaim()
    {
        // Arrange
        var user = CreateTestUser();
        user.Role = null;

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        var roleClaim = token.Claims.FirstOrDefault(c => c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("0", roleClaim.Value);
    }

    [Fact]
    public void GenerateToken_WithUserHavingNullBranchId_UsesZeroForBranchIdClaim()
    {
        // Arrange
        var user = CreateTestUser();
        user.BranchId = null;

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        var branchIdClaim = token.Claims.FirstOrDefault(c => c.Type == "branchId");
        Assert.NotNull(branchIdClaim);
        Assert.Equal("0", branchIdClaim.Value);
    }

    [Fact]
    public void GenerateToken_WithAdminUser_SetsIsAdminClaimToTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsAdmin = true;

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        var isAdminClaim = token.Claims.FirstOrDefault(c => c.Type == "isAdmin");
        Assert.NotNull(isAdminClaim);
        Assert.Equal("true", isAdminClaim.Value);
    }

    [Fact]
    public void GenerateToken_WithNonAdminUser_SetsIsAdminClaimToFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsAdmin = false;

        // Act
        var result = _service.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        var isAdminClaim = token.Claims.FirstOrDefault(c => c.Type == "isAdmin");
        Assert.NotNull(isAdminClaim);
        Assert.Equal("false", isAdminClaim.Value);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        IConfiguration? configuration = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JwtTokenService(configuration!));
        Assert.Equal("configuration", exception.ParamName);
    }

    private SysUser CreateTestUser()
    {
        return new SysUser
        {
            RowId = 123,
            UserName = "testuser",
            RowDesc = "Test User Arabic",
            RowDescE = "Test User English",
            Password = "hashedpassword",
            Role = 5,
            BranchId = 10,
            IsAdmin = false,
            IsActive = true,
            CreationUser = "admin",
            CreationDate = DateTime.UtcNow
        };
    }
}
