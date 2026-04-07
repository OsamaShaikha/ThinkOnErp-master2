namespace ThinkOnErp.Application.DTOs.Auth;

/// <summary>
/// Data transfer object for refresh token request.
/// </summary>
public class RefreshTokenDto
{
    /// <summary>
    /// The refresh token to validate and use for generating new tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
