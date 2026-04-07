using ThinkOnErp.Application.DTOs.Auth;

namespace ThinkOnErp.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Command for refreshing an access token using a refresh token.
/// </summary>
public class RefreshTokenCommand
{
    /// <summary>
    /// The refresh token to validate and use for generating new tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
