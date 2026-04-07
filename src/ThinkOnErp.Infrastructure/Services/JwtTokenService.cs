using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for generating JWT tokens for authenticated users.
/// Reads JWT configuration settings and creates tokens with required claims.
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Generates a JWT token for an authenticated user with all required claims.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <returns>TokenDto containing the JWT token and expiration time</returns>
    public virtual TokenDto GenerateToken(SysUser user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        // Read JWT settings from configuration
        var secretKey = _configuration["JwtSettings:SecretKey"] 
            ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        var issuer = _configuration["JwtSettings:Issuer"] 
            ?? throw new InvalidOperationException("JWT Issuer is not configured");
        var audience = _configuration["JwtSettings:Audience"] 
            ?? throw new InvalidOperationException("JWT Audience is not configured");
        var expiryInMinutes = int.Parse(_configuration["JwtSettings:ExpiryInMinutes"] 
            ?? throw new InvalidOperationException("JWT ExpiryInMinutes is not configured"));

        // Create security key from secret
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Calculate expiration time
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryInMinutes);

        // Create claims with user information
        var claims = new[]
        {
            new Claim("userId", user.RowId.ToString()),
            new Claim("userName", user.UserName),
            new Claim("role", user.Role?.ToString() ?? "0"),
            new Claim("branchId", user.BranchId?.ToString() ?? "0"),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower())
        };

        // Create JWT token
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        // Generate token string
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return new TokenDto
        {
            AccessToken = tokenString,
            ExpiresAt = expiresAt,
            TokenType = "Bearer",
            RefreshToken = GenerateRefreshToken(),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpiryInDays"] ?? "7"))
        };
    }

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    /// </summary>
    /// <returns>Base64 encoded refresh token string</returns>
    public virtual string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// Validates a JWT token and extracts the user ID claim.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>User ID if token is valid, null otherwise</returns>
    public virtual long? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var secretKey = _configuration["JwtSettings:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["JwtSettings:Audience"],
                ValidateLifetime = false, // Don't validate expiry for refresh token scenario
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "userId");

            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
            {
                return userId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
