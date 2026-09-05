using ThinkOnErp.Application.DTOs.User;

namespace ThinkOnErp.Application.DTOs.Auth;

/// <summary>
/// Data transfer object for JWT authentication token response.
/// Returned from successful login operations.
/// </summary>
public class TokenDto
{
    /// <summary>
    /// JWT access token containing user claims
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration timestamp (UTC)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Token type (always "Bearer" for JWT tokens)
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token expiration timestamp (UTC)
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Company code the user belongs to
    /// </summary>
    public string? CompanyCode { get; set; }

    /// <summary>
    /// Oracle schema name for this tenant
    /// </summary>
    public string? CompanySchema { get; set; }

    /// <summary>
    /// Active session language ID (1 = Arabic, 2 = English, 3 = French, 4 = Spanish, 5 = Turkish, 6 = German)
    /// </summary>
    public int Language { get; set; } = 1;

    /// <summary>
    /// Authenticated user information
    /// </summary>
    public UserDto? User { get; set; }
}
