using MediatR;
using ThinkOnErp.Application.DTOs.Auth;

namespace ThinkOnErp.Application.Features.Auth.Commands.Login;

/// <summary>
/// Command to authenticate a user and generate a JWT token.
/// Returns a TokenDto with access token and expiration.
/// </summary>
public class LoginCommand : IRequest<TokenDto?>
{
    /// <summary>
    /// Username for authentication (required)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication (required)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional session language ID (1 = Arabic, 2 = English, 3 = French, 4 = Spanish, 5 = Turkish, 6 = German).
    /// If omitted, falls back to the user's default language.
    /// </summary>
    public int? Language { get; set; }
}
